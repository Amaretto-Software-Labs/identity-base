using System;
using System.Threading;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationContextAccessor : IOrganizationContextAccessor
{
    private sealed class Scope : IDisposable
    {
        private readonly OrganizationContextAccessor _accessor;
        private readonly Holder? _prior;
        private bool _disposed;

        public Scope(OrganizationContextAccessor accessor, Holder? prior)
        {
            _accessor = accessor;
            _prior = prior;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _accessor._current.Value = _prior;
            _disposed = true;
        }
    }

    private sealed class Holder
    {
        public Holder(IOrganizationContext context, Holder? parent)
        {
            Context = context;
            Parent = parent;
        }

        public IOrganizationContext Context { get; }

        public Holder? Parent { get; }
    }

    private readonly AsyncLocal<Holder?> _current = new();

    public IOrganizationContext Current => _current.Value?.Context ?? OrganizationContext.None;

    public IDisposable BeginScope(IOrganizationContext organizationContext)
    {
        organizationContext ??= OrganizationContext.None;

        var holder = new Holder(organizationContext, _current.Value);
        _current.Value = holder;
        return new Scope(this, holder.Parent);
    }
}
