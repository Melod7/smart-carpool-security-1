using Kubix.Domain;

namespace Kubix.Tests;

public class PruebasHumo
{
    [Fact]
    public void Marcador_de_ensamblado_de_dominio_existe()
    {
        Assert.Equal("Kubix.Domain", typeof(DomainAssemblyMarker).Namespace);
    }
}
