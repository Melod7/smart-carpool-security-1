namespace Kubix.Application.EcoTokens;

public interface IServicioEcoTokens
{
    Task<ResumenEcoDto> ObtenerResumenAsync(
        Guid usuarioId,
        int pagina = 1,
        int tamanoPagina = 20,
        CancellationToken ct = default);
}
