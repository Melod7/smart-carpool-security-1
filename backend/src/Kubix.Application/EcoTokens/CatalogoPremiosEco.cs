namespace Kubix.Application.EcoTokens;

/// <summary>Catálogo fijo y mínimo de premios canjeables con EcoTokensUTN.</summary>
public static class CatalogoPremiosEco
{
    public static IReadOnlyList<PremioEcoDto> Todos { get; } =
    [
        new() { Codigo = "gorra", Nombre = "Gorra UTN", Costo = 40 },
        new() { Codigo = "camiseta", Nombre = "Camiseta UTN", Costo = 80 },
        new() { Codigo = "mochila", Nombre = "Mochila UTN", Costo = 120 }
    ];

    public static PremioEcoDto? Buscar(string codigo) =>
        Todos.FirstOrDefault(p =>
            string.Equals(p.Codigo, codigo.Trim(), StringComparison.OrdinalIgnoreCase));
}
