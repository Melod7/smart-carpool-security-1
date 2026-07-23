namespace Kubix.Application.EcoTokens;

public interface IServicioEcoTokens
{
    Task<ResumenEcoDto> ObtenerResumenAsync(
        Guid usuarioId,
        int pagina = 1,
        int tamanoPagina = 20,
        CancellationToken ct = default);

    Task<ResultadoCanjePremioDto> CanjearPremioAsync(
        Guid usuarioId,
        string codigoPremio,
        CancellationToken ct = default);

    Task<IReadOnlyList<CanjeAdminDto>> ListarCanjesAdminAsync(
        int limite = 50,
        CancellationToken ct = default);
}
