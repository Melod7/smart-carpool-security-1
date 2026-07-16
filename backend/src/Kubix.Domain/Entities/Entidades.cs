using Kubix.Domain.Enums;

namespace Kubix.Domain.Entities;

public abstract class EntidadAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ActualizadoEn { get; set; } = DateTimeOffset.UtcNow;
}

public class Universidad : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public EstadoUniversidad Estado { get; set; } = EstadoUniversidad.Activa;

    public ConfiguracionUniversidad? Configuracion { get; set; }
    public ICollection<Campus> Sedes { get; set; } = new List<Campus>();
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}

public class Campus : EntidadAuditable
{
    public Guid UniversidadId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }

    public Universidad Universidad { get; set; } = null!;
}

public class ConfiguracionUniversidad
{
    public Guid UniversidadId { get; set; }
    public string? DominioCorreoPermitido { get; set; }
    public string ZonaHoraria { get; set; } = "America/Guayaquil";
    public string CorreoSoporte { get; set; } = "soporte@kubix.local";
    public int MaxViajesDiariosPorConductor { get; set; } = 6;
    public decimal CalificacionMinimaConductor { get; set; } = 3.5m;
    public decimal FactorCo2KgKm { get; set; } = 0.21m;
    public bool GamificacionHabilitada { get; set; } = true;
    public bool SeguimientoCo2Habilitado { get; set; } = true;
    public bool NotificarSos { get; set; } = true;
    public bool NotificarBloqueo { get; set; } = true;
    public bool NotificarReporteSemanal { get; set; } = true;
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ActualizadoEn { get; set; } = DateTimeOffset.UtcNow;

    public Universidad Universidad { get; set; } = null!;
}

public class Usuario : EntidadAuditable
{
    public Guid? UniversidadId { get; set; }
    public Guid? CampusId { get; set; }
    public RolUsuario Rol { get; set; }
    public EstadoUsuario Estado { get; set; } = EstadoUsuario.Activo;
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string HashContrasena { get; set; } = string.Empty;
    public GeneroUsuario? Genero { get; set; }
    public string? ImagenPerfil { get; set; }
    public string? Carrera { get; set; }
    public string? NumeroIdentificacion { get; set; }
    public decimal PromedioCalificacion { get; set; }
    public int BalanceEco { get; set; }
    public int EcoVitalicio { get; set; }
    public bool DebeCambiarContrasena { get; set; }

    public Universidad? Universidad { get; set; }
    public Campus? Campus { get; set; }
    public ICollection<Vehiculo> Vehiculos { get; set; } = new List<Vehiculo>();
    public ICollection<TokenRefresco> TokensRefresco { get; set; } = new List<TokenRefresco>();
    public ICollection<ContactoEmergencia> ContactosEmergencia { get; set; } = new List<ContactoEmergencia>();
}

public class TokenRefresco
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string HashToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiraEn { get; set; }
    public DateTimeOffset? RevocadoEn { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public Usuario Usuario { get; set; } = null!;
}

public class SolicitudRegistro : EntidadAuditable
{
    public Guid UniversidadId { get; set; }
    public Guid CampusId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string HashContrasena { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public GeneroUsuario? Genero { get; set; }
    public string? ImagenPerfil { get; set; }
    public string? Carrera { get; set; }
    public string? NumeroIdentificacion { get; set; }
    public string? VehiculoJson { get; set; }
    public EstadoSolicitudRegistro Estado { get; set; } = EstadoSolicitudRegistro.Pendiente;
    public Guid? DecididoPor { get; set; }
    public DateTimeOffset? DecididoEn { get; set; }

    public Universidad Universidad { get; set; } = null!;
    public Campus Campus { get; set; } = null!;
}

public class Vehiculo : EntidadAuditable
{
    public Guid UniversidadId { get; set; }
    public Guid UsuarioId { get; set; }
    public string MarcaModelo { get; set; } = string.Empty;
    public string Placa { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int AsientosTotales { get; set; }
    public string? Imagen { get; set; }

    public Universidad Universidad { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}

public class Viaje : EntidadAuditable
{
    public Guid UniversidadId { get; set; }
    public Guid ConductorId { get; set; }
    public Guid CampusDestinoId { get; set; }
    public string OrigenTexto { get; set; } = string.Empty;
    public double OrigenLat { get; set; }
    public double OrigenLng { get; set; }
    public DateTimeOffset SaleEn { get; set; }
    public int AsientosDisponibles { get; set; }
    public string? Polilinea { get; set; }
    public decimal DistanciaKm { get; set; }
    public EstadoViaje Estado { get; set; } = EstadoViaje.Programado;
    public DateTimeOffset? IniciadoEn { get; set; }
    public DateTimeOffset? CompletadoEn { get; set; }
    public decimal Co2AhorradoKg { get; set; }

    public Universidad Universidad { get; set; } = null!;
    public Usuario Conductor { get; set; } = null!;
    public Campus CampusDestino { get; set; } = null!;
    public ICollection<PuntoRutaViaje> PuntosRuta { get; set; } = new List<PuntoRutaViaje>();
    public ICollection<SolicitudViaje> SolicitudesViaje { get; set; } = new List<SolicitudViaje>();
    public ICollection<PingUbicacion> PingsUbicacion { get; set; } = new List<PingUbicacion>();
    public ICollection<Calificacion> Calificaciones { get; set; } = new List<Calificacion>();
}

public class PuntoRutaViaje
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UniversidadId { get; set; }
    public Guid ViajeId { get; set; }
    public int Seq { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Etiqueta { get; set; }

    public Universidad Universidad { get; set; } = null!;
    public Viaje Viaje { get; set; } = null!;
}

public class SolicitudViaje : EntidadAuditable
{
    public Guid UniversidadId { get; set; }
    public Guid ViajeId { get; set; }
    public Guid PasajeroId { get; set; }
    public string RecogidaTexto { get; set; } = string.Empty;
    public double RecogidaLat { get; set; }
    public double RecogidaLng { get; set; }
    public double? LatSugerida { get; set; }
    public double? LngSugerida { get; set; }
    public double? DistanciaARutaM { get; set; }
    public EstadoSolicitudViaje Estado { get; set; } = EstadoSolicitudViaje.Pendiente;

    public Universidad Universidad { get; set; } = null!;
    public Viaje Viaje { get; set; } = null!;
    public Usuario Pasajero { get; set; } = null!;
}

public class Calificacion : EntidadAuditable
{
    public Guid UniversidadId { get; set; }
    public Guid ViajeId { get; set; }
    public Guid CalificadorId { get; set; }
    public Guid CalificadoId { get; set; }
    public int Estrellas { get; set; }
    public string? Comentario { get; set; }

    public Universidad Universidad { get; set; } = null!;
    public Viaje Viaje { get; set; } = null!;
    public Usuario Calificador { get; set; } = null!;
    public Usuario Calificado { get; set; } = null!;
}

public class AlertaSos : EntidadAuditable
{
    public Guid UniversidadId { get; set; }
    public Guid? ViajeId { get; set; }
    public Guid UsuarioId { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public DateTimeOffset DisparadaEn { get; set; } = DateTimeOffset.UtcNow;
    public EstadoAlertaSos Estado { get; set; } = EstadoAlertaSos.Activa;
    public Guid? ResueltaPor { get; set; }
    public DateTimeOffset? ResueltaEn { get; set; }

    public Universidad Universidad { get; set; } = null!;
    public Viaje? Viaje { get; set; }
    public Usuario Usuario { get; set; } = null!;
}

public class PingUbicacion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UniversidadId { get; set; }
    public Guid ViajeId { get; set; }
    public Guid UsuarioId { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public DateTimeOffset RegistradoEn { get; set; } = DateTimeOffset.UtcNow;

    public Universidad Universidad { get; set; } = null!;
    public Viaje Viaje { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}

public class EventoAuditoria
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UniversidadId { get; set; }
    public Guid? UsuarioId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public TipoEventoAuditoria Tipo { get; set; }
    public SeveridadAuditoria Severidad { get; set; }
    public string? Ip { get; set; }
    public string? Dispositivo { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public Universidad? Universidad { get; set; }
    public Usuario? Usuario { get; set; }
}

public class Notificacion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UniversidadId { get; set; }
    public Guid? UsuarioDestinatarioId { get; set; }
    public RolUsuario? RolDestinatario { get; set; }
    public TipoNotificacion Tipo { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Cuerpo { get; set; } = string.Empty;
    public bool Leida { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public Universidad Universidad { get; set; } = null!;
    public Usuario? UsuarioDestinatario { get; set; }
}

public class ContactoEmergencia : EntidadAuditable
{
    public Guid UniversidadId { get; set; }
    public Guid UsuarioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Relacion { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;

    public Universidad Universidad { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}

public class TransaccionEcoToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UniversidadId { get; set; }
    public Guid UsuarioId { get; set; }
    public TipoTransaccionEcoToken Tipo { get; set; }
    public int Monto { get; set; }
    public string IdFuente { get; set; } = string.Empty;
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public Universidad Universidad { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}
