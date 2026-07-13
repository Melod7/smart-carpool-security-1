using Kubix.Application.Tenancy;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;

namespace Kubix.Infrastructure.Tenancy;

public sealed class EscritorAuditoria(
    ContextoApp db,
    IContextoInquilino inquilino) : IEscritorAuditoria
{
    public async Task EscribirAsync(
        string accion,
        TipoEventoAuditoria tipo,
        SeveridadAuditoria severidad,
        Guid? universidadId = null,
        Guid? usuarioId = null,
        string? ip = null,
        string? dispositivo = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accion);

        db.EventosAuditoria.Add(new EventoAuditoria
        {
            UniversidadId = universidadId ?? inquilino.UniversidadId,
            UsuarioId = usuarioId ?? inquilino.UsuarioId,
            Accion = accion.Trim(),
            Tipo = tipo,
            Severidad = severidad,
            Ip = ip,
            Dispositivo = dispositivo,
            CreadoEn = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }
}
