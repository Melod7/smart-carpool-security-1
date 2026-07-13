using Kubix.Application.EcoTokens;

namespace Kubix.Infrastructure.EcoTokens;

/// <summary>
/// Stub no-op hasta KBX-31. Los hooks de complete / late-cancel / rating
/// quedan cableados para que el motor real los reemplace sin tocar cicloviaje.
/// </summary>
public sealed class MotorEcoTokensStub : IMotorEcoTokens
{
    public Task AlCompletarViajeAsync(Guid viajeId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task AlCancelacionTardiaAsync(
        Guid viajeId,
        Guid conductorId,
        CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task AlCalificarAsync(Guid calificacionId, CancellationToken ct = default) =>
        Task.CompletedTask;
}
