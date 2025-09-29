using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Options;

internal sealed class IdentityBaseModelCustomizationOptionsExtension : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public IdentityBaseModelCustomizationOptionsExtension(IdentityBaseModelCustomizationOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public IdentityBaseModelCustomizationOptions Options { get; }

    public void ApplyServices(IServiceCollection services)
    {
        // no-op; customization delegates are applied directly in OnModelCreating
    }

    public void Validate(IDbContextOptions options)
    {
    }

    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
    {
        public ExtensionInfo(IdentityBaseModelCustomizationOptionsExtension extension)
            : base(extension)
        {
        }

        private new IdentityBaseModelCustomizationOptionsExtension Extension
            => (IdentityBaseModelCustomizationOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider => false;

        public override string LogFragment => string.Empty;

        public override int GetServiceProviderHashCode()
            => Extension.Options.GetHashCode();

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
        }

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;
    }
}
