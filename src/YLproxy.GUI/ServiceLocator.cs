using System;
using Microsoft.Extensions.DependencyInjection;

namespace YLproxy.GUI;

/// <summary>
/// Temporary global holder for ServiceProvider.
/// Phase A 先保证可运行：后续在 Phase 里可以进一步移除/替换。
/// </summary>
public static class ServiceLocator
{
    private static IServiceProvider? _provider;

    public static void SetProvider(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public static T GetRequiredService<T>() where T : notnull
    {
        if (_provider is null)
            throw new InvalidOperationException("DI provider not initialized.");

        return _provider.GetRequiredService<T>();
    }
}


