using Kubix.Domain.Enums;

namespace Kubix.Application.Tenancy;

public interface IEscritorAuditoria
{
    Task EscribirAsync(
        string accion,
        TipoEventoAuditoria tipo,
        SeveridadAuditoria severidad,
        Guid? universidadId = null,
        Guid? usuarioId = null,
        string? ip = null,
        string? dispositivo = null,
        CancellationToken ct = default);
}
