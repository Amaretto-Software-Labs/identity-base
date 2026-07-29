using Identity.Base.Data;
using Identity.Base.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Features.Authentication.Passkeys;

internal sealed class PasskeyDraftCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<PasskeyOptions> options,
    ILogger<PasskeyDraftCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ConsumedRetention = TimeSpan.FromHours(1);
    private const int CleanupBatchSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        await CleanupAsync(stoppingToken);
        using var timer = new PeriodicTimer(CleanupInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupAsync(stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTimeOffset.UtcNow;
            var consumedBefore = now.Subtract(ConsumedRetention);

            if (dbContext.Database.IsRelational())
            {
                await dbContext.PasskeyRegistrationDrafts
                    .Where(draft => draft.ExpiresAt < now ||
                                    (draft.ConsumedAt != null && draft.ConsumedAt < consumedBefore))
                    .OrderBy(draft => draft.ExpiresAt)
                    .Take(CleanupBatchSize)
                    .ExecuteDeleteAsync(cancellationToken);
                await dbContext.PasskeyRecoveryDrafts
                    .Where(draft => draft.ExpiresAt < now ||
                                    (draft.ConsumedAt != null && draft.ConsumedAt < consumedBefore))
                    .OrderBy(draft => draft.ExpiresAt)
                    .Take(CleanupBatchSize)
                    .ExecuteDeleteAsync(cancellationToken);
                return;
            }

            var registrationDrafts = await dbContext.PasskeyRegistrationDrafts
                .Where(draft => draft.ExpiresAt < now ||
                                (draft.ConsumedAt != null && draft.ConsumedAt < consumedBefore))
                .OrderBy(draft => draft.ExpiresAt)
                .Take(CleanupBatchSize)
                .ToListAsync(cancellationToken);
            var recoveryDrafts = await dbContext.PasskeyRecoveryDrafts
                .Where(draft => draft.ExpiresAt < now ||
                                (draft.ConsumedAt != null && draft.ConsumedAt < consumedBefore))
                .OrderBy(draft => draft.ExpiresAt)
                .Take(CleanupBatchSize)
                .ToListAsync(cancellationToken);
            dbContext.RemoveRange(registrationDrafts);
            dbContext.RemoveRange(recoveryDrafts);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to clean up expired passkey drafts.");
        }
    }
}
