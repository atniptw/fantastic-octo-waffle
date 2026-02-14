using System.Diagnostics.CodeAnalysis;

namespace BlazorApp.Pages;

internal static class RoutePreserver
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(Index))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(ModDetail))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Viewer3D))]
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void Preserve()
    {
    }
}
