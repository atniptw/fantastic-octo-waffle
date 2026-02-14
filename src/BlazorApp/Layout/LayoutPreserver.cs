using System.Diagnostics.CodeAnalysis;

namespace BlazorApp.Layout;

internal static class LayoutPreserver
{
	[DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(MainLayout))]
	[DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(NavMenu))]
	[System.Runtime.CompilerServices.ModuleInitializer]
	public static void Preserve()
	{
	}
}
