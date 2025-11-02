using System;
using System.Threading;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Domain;

namespace Identity.Base.Organisations.Services;

public sealed class OrganisationContextAccessor : IOrganisationContextAccessor
{
    private sealed class Scope : IDisposable
    {
        private readonly OrganisationContextAccessor _accessor;
        private readonly Holder? _prior;
        private bool _disposed;

        public Scope(OrganisationContextAccessor accessor, Holder? prior)
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
        public Holder(IOrganisationContext context, Holder? parent)
        {
            Context = context;
            Parent = parent;
        }

        public IOrganisationContext Context { get; }

        public Holder? Parent { get; }
    }

    private readonly AsyncLocal<Holder?> _current = new();

    public IOrganisationContext Current => _current.Value?.Context ?? OrganisationContext.None;

    public IDisposable BeginScope(IOrganisationContext organisationContext)
    {
        organisationContext ??= OrganisationContext.None;

        var holder = new Holder(organisationContext, _current.Value);
        _current.Value = holder;
        return new Scope(this, holder.Parent);
    }
}
