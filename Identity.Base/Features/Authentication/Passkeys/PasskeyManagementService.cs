using System.Data;
using Identity.Base.Data;
using Identity.Base.Identity;
using Identity.Base.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.Passkeys;

internal sealed class PasskeyManagementService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IOptions<PasskeyOptions> options)
{
    private readonly PasskeyOptions _options = options.Value;

    public async Task<IReadOnlyCollection<PasskeySummary>> ListAsync(ApplicationUser user)
    {
        var passkeys = await userManager.GetPasskeysAsync(user);
        var stamps = await dbContext.Set<ApplicationUserPasskey>()
            .Where(entity => entity.UserId == user.Id)
            .ToDictionaryAsync(
                entity => PasskeyEncoding.CredentialId(entity.CredentialId),
                entity => entity.ConcurrencyStamp,
                StringComparer.Ordinal);

        return passkeys
            .Select(passkey => ToSummary(
                passkey,
                stamps.GetValueOrDefault(PasskeyEncoding.CredentialId(passkey.CredentialId)) ?? string.Empty))
            .OrderByDescending(passkey => passkey.CreatedAt)
            .ToArray();
    }

    public async Task<string?> MakeCreationOptionsAsync(ApplicationUser user)
    {
        var passkeys = await userManager.GetPasskeysAsync(user);
        if (passkeys.Count >= _options.MaxPasskeysPerUser)
        {
            return null;
        }

        return await signInManager.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
        {
            Id = user.Id.ToString(),
            Name = user.Email ?? user.UserName ?? "User",
            DisplayName = user.DisplayName ?? user.Email ?? user.UserName ?? "User"
        });
    }

    public async Task<PasskeyMutationResult> AddAsync(
        ApplicationUser user,
        string name,
        string credentialJson,
        CancellationToken cancellationToken)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Length > _options.NameMaxLength)
        {
            return PasskeyMutationResult.InvalidName;
        }

        if ((await userManager.GetPasskeysAsync(user)).Count >= _options.MaxPasskeysPerUser)
        {
            return PasskeyMutationResult.LimitReached;
        }

        var attestation = await signInManager.PerformPasskeyAttestationAsync(credentialJson);
        if (!attestation.Succeeded ||
            attestation.Passkey is null ||
            attestation.UserEntity is null ||
            !string.Equals(attestation.UserEntity.Id, user.Id.ToString(), StringComparison.Ordinal))
        {
            return PasskeyMutationResult.Failed;
        }

        attestation.Passkey.Name = name;

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if ((await userManager.GetPasskeysAsync(user)).Count >= _options.MaxPasskeysPerUser)
        {
            return PasskeyMutationResult.LimitReached;
        }

        var result = await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
        if (!result.Succeeded)
        {
            return PasskeyMutationResult.Failed;
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        var entity = await dbContext.Set<ApplicationUserPasskey>()
            .AsNoTracking()
            .SingleAsync(
                passkey => passkey.UserId == user.Id &&
                           passkey.CredentialId == attestation.Passkey.CredentialId,
                cancellationToken);
        return PasskeyMutationResult.Success(ToSummary(attestation.Passkey, entity.ConcurrencyStamp));
    }

    public async Task<PasskeyMutationResult> RenameAsync(
        ApplicationUser user,
        byte[] credentialId,
        string name,
        string concurrencyStamp,
        CancellationToken cancellationToken)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Length > _options.NameMaxLength)
        {
            return PasskeyMutationResult.InvalidName;
        }

        var entity = await dbContext.Set<ApplicationUserPasskey>()
            .SingleOrDefaultAsync(
                passkey => passkey.UserId == user.Id && passkey.CredentialId == credentialId,
                cancellationToken);
        if (entity is null)
        {
            return PasskeyMutationResult.NotFound;
        }

        if (!string.Equals(entity.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
        {
            return PasskeyMutationResult.Modified;
        }

        var passkey = await userManager.GetPasskeyAsync(user, credentialId);
        if (passkey is null)
        {
            return PasskeyMutationResult.NotFound;
        }

        passkey.Name = name;
        var result = await userManager.AddOrUpdatePasskeyAsync(user, passkey);
        if (!result.Succeeded)
        {
            return result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure))
                ? PasskeyMutationResult.Modified
                : PasskeyMutationResult.Failed;
        }

        var updated = await dbContext.Set<ApplicationUserPasskey>()
            .AsNoTracking()
            .SingleAsync(
                stored => stored.UserId == user.Id && stored.CredentialId == credentialId,
                cancellationToken);
        return PasskeyMutationResult.Success(ToSummary(passkey, updated.ConcurrencyStamp));
    }

    public async Task<PasskeyMutationResult> RemoveAsync(
        ApplicationUser user,
        byte[] credentialId,
        CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var passkeys = await userManager.GetPasskeysAsync(user);
        if (passkeys.All(passkey => !passkey.CredentialId.SequenceEqual(credentialId)))
        {
            return PasskeyMutationResult.NotFound;
        }

        var hasOtherLogin =
            passkeys.Count > 1 ||
            await userManager.HasPasswordAsync(user) ||
            (await userManager.GetLoginsAsync(user)).Count > 0;
        if (!hasOtherLogin)
        {
            return PasskeyMutationResult.LoginMethodRequired;
        }

        var result = await userManager.RemovePasskeyAsync(user, credentialId);
        if (!result.Succeeded)
        {
            return PasskeyMutationResult.Failed;
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return PasskeyMutationResult.Success();
    }

    private static PasskeySummary ToSummary(UserPasskeyInfo passkey, string concurrencyStamp)
        => new(
            PasskeyEncoding.CredentialId(passkey.CredentialId),
            passkey.Name ?? "Passkey",
            passkey.CreatedAt,
            passkey.Transports ?? [],
            passkey.IsBackupEligible,
            passkey.IsBackedUp,
            concurrencyStamp);
}

internal enum PasskeyMutationStatus
{
    Success,
    InvalidName,
    LimitReached,
    Failed,
    NotFound,
    Modified,
    LoginMethodRequired
}

internal sealed record PasskeyMutationResult(
    PasskeyMutationStatus Status,
    PasskeySummary? Passkey = null)
{
    public static PasskeyMutationResult InvalidName { get; } = new(PasskeyMutationStatus.InvalidName);
    public static PasskeyMutationResult LimitReached { get; } = new(PasskeyMutationStatus.LimitReached);
    public static PasskeyMutationResult Failed { get; } = new(PasskeyMutationStatus.Failed);
    public static PasskeyMutationResult NotFound { get; } = new(PasskeyMutationStatus.NotFound);
    public static PasskeyMutationResult Modified { get; } = new(PasskeyMutationStatus.Modified);
    public static PasskeyMutationResult LoginMethodRequired { get; } = new(PasskeyMutationStatus.LoginMethodRequired);

    public static PasskeyMutationResult Success(PasskeySummary? passkey = null)
        => new(PasskeyMutationStatus.Success, passkey);
}
