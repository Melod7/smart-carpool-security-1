using Kubix.Application.EcoTokens;

namespace Kubix.Infrastructure.EcoTokens;

/// <summary>
/// Stub no-op para pruebas que no ejercen accrual. Producción usa <see cref="MotorEcoTokens"/>.
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
