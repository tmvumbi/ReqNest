using ReqNest.Core.Storage;

namespace ReqNest.Tests.Architecture;

public sealed class CoreArchitectureTests
{
    [Fact]
    public void Core_does_not_reference_outer_layers()
    {
        var references = typeof(IBlobStorageService)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("ReqNest.Api", references);
        Assert.DoesNotContain("ReqNest.Infrastructure", references);
    }
}
