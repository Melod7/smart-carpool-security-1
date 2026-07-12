using Kubix.Domain.Enums;

namespace Kubix.Domain;

/// <summary>
/// Convierte miembros de enum en español a los valores snake_case en inglés
/// que se almacenan en PostgreSQL (y viceversa).
/// </summary>
public static class ConversorEnumDominio
{
    private static readonly Dictionary<EstadoUniversidad, string> EstadoUniversidadADb = new()
    {
        [EstadoUniversidad.Activa] = "active",
        [EstadoUniversidad.Suspendida] = "suspended"
    };

    private static readonly Dictionary<RolUsuario, string> RolUsuarioADb = new()
    {
        [RolUsuario.SuperAdministrador] = "super_admin",
        [RolUsuario.Coordinador] = "coordinador",
        [RolUsuario.Conductor] = "driver",
        [RolUsuario.Pasajero] = "passenger"
    };

    private static readonly Dictionary<EstadoUsuario, string> EstadoUsuarioADb = new()
    {
        [EstadoUsuario.Activo] = "active",
        [EstadoUsuario.Bloqueado] = "blocked",
        [EstadoUsuario.Pendiente] = "pending"
    };

    private static readonly Dictionary<EstadoSolicitudRegistro, string> EstadoSolicitudRegistroADb = new()
    {
        [EstadoSolicitudRegistro.Pendiente] = "pending",
        [EstadoSolicitudRegistro.Aceptada] = "accepted",
        [EstadoSolicitudRegistro.Denegada] = "denied"
    };

    private static readonly Dictionary<EstadoViaje, string> EstadoViajeADb = new()
    {
        [EstadoViaje.Programado] = "scheduled",
        [EstadoViaje.EnCurso] = "in_progress",
        [EstadoViaje.Completado] = "completed",
        [EstadoViaje.Cancelado] = "cancelled"
    };

    private static readonly Dictionary<EstadoSolicitudViaje, string> EstadoSolicitudViajeADb = new()
    {
        [EstadoSolicitudViaje.Pendiente] = "pending",
        [EstadoSolicitudViaje.Aceptada] = "accepted",
        [EstadoSolicitudViaje.Rechazada] = "rejected",
        [EstadoSolicitudViaje.CanceladaPorPasajero] = "cancelled_by_passenger",
        [EstadoSolicitudViaje.CanceladaPorConductor] = "cancelled_by_driver"
    };

    private static readonly Dictionary<EstadoAlertaSos, string> EstadoAlertaSosADb = new()
    {
        [EstadoAlertaSos.Activa] = "active",
        [EstadoAlertaSos.Resuelta] = "resolved"
    };

    private static readonly Dictionary<TipoEventoAuditoria, string> TipoEventoAuditoriaADb = new()
    {
        [TipoEventoAuditoria.Sos] = "sos",
        [TipoEventoAuditoria.Auth] = "auth",
        [TipoEventoAuditoria.Admin] = "admin",
        [TipoEventoAuditoria.Sistema] = "system"
    };

    private static readonly Dictionary<SeveridadAuditoria, string> SeveridadAuditoriaADb = new()
    {
        [SeveridadAuditoria.Alta] = "high",
        [SeveridadAuditoria.Media] = "medium",
        [SeveridadAuditoria.Baja] = "low"
    };

    private static readonly Dictionary<TipoNotificacion, string> TipoNotificacionADb = new()
    {
        [TipoNotificacion.Sos] = "sos",
        [TipoNotificacion.Bloqueo] = "block",
        [TipoNotificacion.Auth] = "auth",
        [TipoNotificacion.Reporte] = "report",
        [TipoNotificacion.Sistema] = "system"
    };

    private static readonly Dictionary<TipoTransaccionEcoToken, string> TipoTransaccionEcoTokenADb = new()
    {
        [TipoTransaccionEcoToken.ViajeCompletadoConductor] = "trip_completed_driver",
        [TipoTransaccionEcoToken.ViajeCompletadoPasajero] = "trip_completed_passenger",
        [TipoTransaccionEcoToken.CalificacionEnviada] = "rating_submitted",
        [TipoTransaccionEcoToken.RachaSemanal] = "weekly_streak",
        [TipoTransaccionEcoToken.PenalizacionCancelacionTardia] = "late_cancel_penalty"
    };

    public static string ACadenaDb<TEnum>(TEnum valor) where TEnum : struct, Enum
    {
        if (valor is EstadoUniversidad eu) return Buscar(EstadoUniversidadADb, eu);
        if (valor is RolUsuario ru) return Buscar(RolUsuarioADb, ru);
        if (valor is EstadoUsuario esu) return Buscar(EstadoUsuarioADb, esu);
        if (valor is EstadoSolicitudRegistro esr) return Buscar(EstadoSolicitudRegistroADb, esr);
        if (valor is EstadoViaje ev) return Buscar(EstadoViajeADb, ev);
        if (valor is EstadoSolicitudViaje esv) return Buscar(EstadoSolicitudViajeADb, esv);
        if (valor is EstadoAlertaSos eas) return Buscar(EstadoAlertaSosADb, eas);
        if (valor is TipoEventoAuditoria tea) return Buscar(TipoEventoAuditoriaADb, tea);
        if (valor is SeveridadAuditoria sa) return Buscar(SeveridadAuditoriaADb, sa);
        if (valor is TipoNotificacion tn) return Buscar(TipoNotificacionADb, tn);
        if (valor is TipoTransaccionEcoToken tte) return Buscar(TipoTransaccionEcoTokenADb, tte);

        throw new ArgumentOutOfRangeException(nameof(valor), valor, $"Enum no mapeado: {typeof(TEnum).Name}");
    }

    public static TEnum DesdeCadenaDb<TEnum>(string valor) where TEnum : struct, Enum
    {
        if (typeof(TEnum) == typeof(EstadoUniversidad))
            return (TEnum)(object)BuscarInverso(EstadoUniversidadADb, valor);
        if (typeof(TEnum) == typeof(RolUsuario))
            return (TEnum)(object)BuscarInverso(RolUsuarioADb, valor);
        if (typeof(TEnum) == typeof(EstadoUsuario))
            return (TEnum)(object)BuscarInverso(EstadoUsuarioADb, valor);
        if (typeof(TEnum) == typeof(EstadoSolicitudRegistro))
            return (TEnum)(object)BuscarInverso(EstadoSolicitudRegistroADb, valor);
        if (typeof(TEnum) == typeof(EstadoViaje))
            return (TEnum)(object)BuscarInverso(EstadoViajeADb, valor);
        if (typeof(TEnum) == typeof(EstadoSolicitudViaje))
            return (TEnum)(object)BuscarInverso(EstadoSolicitudViajeADb, valor);
        if (typeof(TEnum) == typeof(EstadoAlertaSos))
            return (TEnum)(object)BuscarInverso(EstadoAlertaSosADb, valor);
        if (typeof(TEnum) == typeof(TipoEventoAuditoria))
            return (TEnum)(object)BuscarInverso(TipoEventoAuditoriaADb, valor);
        if (typeof(TEnum) == typeof(SeveridadAuditoria))
            return (TEnum)(object)BuscarInverso(SeveridadAuditoriaADb, valor);
        if (typeof(TEnum) == typeof(TipoNotificacion))
            return (TEnum)(object)BuscarInverso(TipoNotificacionADb, valor);
        if (typeof(TEnum) == typeof(TipoTransaccionEcoToken))
            return (TEnum)(object)BuscarInverso(TipoTransaccionEcoTokenADb, valor);

        throw new ArgumentOutOfRangeException(nameof(valor), valor, $"Enum no mapeado: {typeof(TEnum).Name}");
    }

    private static string Buscar<TEnum>(Dictionary<TEnum, string> mapa, TEnum clave)
        where TEnum : struct, Enum
    {
        if (mapa.TryGetValue(clave, out var valor))
        {
            return valor;
        }

        throw new ArgumentOutOfRangeException(nameof(clave), clave, $"Valor de enum sin mapeo DB: {typeof(TEnum).Name}.{clave}");
    }

    private static TEnum BuscarInverso<TEnum>(Dictionary<TEnum, string> mapa, string valorDb)
        where TEnum : struct, Enum
    {
        foreach (var par in mapa)
        {
            if (string.Equals(par.Value, valorDb, StringComparison.Ordinal))
            {
                return par.Key;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(valorDb), valorDb, $"Valor DB inválido para {typeof(TEnum).Name}");
    }
}
