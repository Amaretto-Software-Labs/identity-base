using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationRoleSeeder
{
    private readonly OrganizationDbContext _dbContext;
    private readonly OrganizationRoleOptions _options;
    private readonly IdentityBaseSeedCallbacks _seedCallbacks;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrganizationRoleSeeder>? _logger;

    public OrganizationRoleSeeder(
        OrganizationDbContext dbContext,
        IOptions<OrganizationRoleOptions> options,
        IdentityBaseSeedCallbacks seedCallbacks,
        IServiceProvider serviceProvider,
        ILogger<OrganizationRoleSeeder>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _seedCallbacks = seedCallbacks ?? throw new ArgumentNullException(nameof(seedCallbacks));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new List<(string Name, string Description)>
        {
            (_options.OwnerRoleName, "Organization owner: full management access."),
            (_options.ManagerRoleName, "Organization manager: manage members and settings."),
            (_options.MemberRoleName, "Organization member: default access level.")
        };

        var created = 0;
        foreach (var (name, description) in defaults)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var exists = await _dbContext.OrganizationRoles
                .AnyAsync(role => role.OrganizationId == null && role.TenantId == null && role.Name == name, cancellationToken)
                .ConfigureAwait(false);

            if (exists)
            {
                continue;
            }

            _dbContext.OrganizationRoles.Add(new OrganizationRole
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                IsSystemRole = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            created++;
        }

        if (created > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger?.LogInformation("Seeded {Count} default organization roles.", created);
        }

        foreach (var callback in _seedCallbacks.OrganizationSeedCallbacks)
        {
            await callback(_serviceProvider, cancellationToken).ConfigureAwait(false);
        }
    }
}
