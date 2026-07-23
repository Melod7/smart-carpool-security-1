namespace Kubix.Domain.Enums;

public enum EstadoUniversidad
{
    Activa,
    Suspendida
}

public enum RolUsuario
{
    SuperAdministrador,
    Coordinador,
    Conductor,
    Pasajero
}

public enum GeneroUsuario
{
    Masculino,
    Femenino
}

public enum EstadoUsuario
{
    Activo,
    Bloqueado,
    Pendiente,
    Eliminado
}

public enum EstadoSolicitudRegistro
{
    Pendiente,
    Aceptada,
    Denegada
}

public enum EstadoViaje
{
    Programado,
    EnCurso,
    Completado,
    Cancelado
}

public enum EstadoSolicitudViaje
{
    Pendiente,
    Aceptada,
    Rechazada,
    CanceladaPorPasajero,
    CanceladaPorConductor
}

public enum EstadoAlertaSos
{
    Activa,
    Resuelta
}

public enum TipoEventoAuditoria
{
    Sos,
    Auth,
    Admin,
    Sistema
}

public enum SeveridadAuditoria
{
    Alta,
    Media,
    Baja
}

public enum TipoNotificacion
{
    Sos,
    Bloqueo,
    Auth,
    Reporte,
    Sistema
}

public enum TipoTransaccionEcoToken
{
    ViajeCompletadoConductor,
    ViajeCompletadoPasajero,
    CalificacionEnviada,
    RachaSemanal,
    PenalizacionCancelacionTardia,
    CanjePremio
}
