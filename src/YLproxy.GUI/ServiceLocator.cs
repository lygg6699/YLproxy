using System;
using Microsoft.Extensions.DependencyInjection;

namespace YLproxy.GUI;

/// <summary>
/// Legacy ServiceLocator — no longer used since Phase B2.
/// All dependencies are resolved via constructor injection.
/// Kept to avoid breaking any external references; will be removed in Phase B cleanup.
/// </summary>
[Obsolete("Use constructor injection instead of ServiceLocator.", error: false)]
public static class ServiceLocator
{
    private static IServiceProvider? _provider;

    [Obsolete("SetProvider is no longer called. Use DI container directly.", error: false)]
    public static void SetProvider(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    [Obsolete("Use constructor injection instead.", error: false)]
    public static T GetRequiredService<T>() where T : notnull
    {
        if (_provider is null)
            throw new InvalidOperationException("DI provider not initialized.");

        return _provider.GetRequiredService<T>();
    }
}


