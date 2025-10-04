using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Identity;

public sealed class IdentityBaseSeedCallbacks
{
    private readonly ConcurrentQueue<Func<IServiceProvider, CancellationToken, Task>> _roleSeedCallbacks = new();
    private readonly ConcurrentQueue<Func<IServiceProvider, CancellationToken, Task>> _identitySeedCallbacks = new();
    private readonly ConcurrentQueue<Func<IServiceProvider, CancellationToken, Task>> _organizationSeedCallbacks = new();

    internal IEnumerable<Func<IServiceProvider, CancellationToken, Task>> RoleSeedCallbacks => _roleSeedCallbacks.ToArray();

    internal IEnumerable<Func<IServiceProvider, CancellationToken, Task>> IdentitySeedCallbacks => _identitySeedCallbacks.ToArray();

    internal IEnumerable<Func<IServiceProvider, CancellationToken, Task>> OrganizationSeedCallbacks => _organizationSeedCallbacks.ToArray();

    public void RegisterRoleSeedCallback(Func<IServiceProvider, CancellationToken, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _roleSeedCallbacks.Enqueue(callback);
    }

    public void RegisterIdentitySeedCallback(Func<IServiceProvider, CancellationToken, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _identitySeedCallbacks.Enqueue(callback);
    }

    public void RegisterOrganizationSeedCallback(Func<IServiceProvider, CancellationToken, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _organizationSeedCallbacks.Enqueue(callback);
    }
}
