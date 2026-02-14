using Xunit;

namespace BlazorApp.E2E.Tests;

/// <summary>
/// Defines a test collection that shares the BlazorServerFixture across all test classes.
/// This ensures the Blazor app is built once and a single server instance is used for all E2E tests.
/// </summary>
[CollectionDefinition("Blazor E2E")]
public class BlazorE2ECollection : ICollectionFixture<BlazorServerFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
