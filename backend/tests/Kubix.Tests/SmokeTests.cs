using Kubix.Domain;

namespace Kubix.Tests;

public class SmokeTests
{
    [Fact]
    public void Domain_assembly_marker_exists()
    {
        Assert.Equal("Kubix.Domain", typeof(DomainAssemblyMarker).Namespace);
    }
}
