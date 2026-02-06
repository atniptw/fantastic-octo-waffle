using Microsoft.Extensions.DependencyInjection;

namespace BlazorApp.Services;

internal static class ServiceLocator
{
    public static IServiceProvider? Provider { get; set; }

    public static T GetRequiredService<T>() where T : notnull
    {
        if (Provider is null)
        {
            throw new InvalidOperationException("Service provider not initialized");
        }

        return Provider.GetRequiredService<T>();
    }
}
