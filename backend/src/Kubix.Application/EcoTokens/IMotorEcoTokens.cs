namespace Kubix.Application.EcoTokens;

public interface IMotorEcoTokens
{
    Task AlCompletarViajeAsync(Guid viajeId, CancellationToken ct = default);

    Task AlCancelacionTardiaAsync(Guid viajeId, Guid conductorId, CancellationToken ct = default);

    Task AlCalificarAsync(Guid calificacionId, CancellationToken ct = default);
}
