using System.Text.Json.Serialization;
using Kubix.Application.Sos;

namespace Kubix.Application.Admin;

/// <summary>
/// KPIs del dashboard admin.
/// <para>
/// <c>adoptionRate</c> = usuarios activos (driver+passenger) / total usuarios
/// (driver+passenger) de la universidad, como porcentaje 0–100.
/// No usa métrica basada en viajes.
/// </para>
/// </summary>
public sealed class DashboardAdminDto
{
    [JsonPropertyName("tripsToday")]
    public int ViajesHoy { get; set; }

    [JsonPropertyName("blockedUsers")]
    public int UsuariosBloqueados { get; set; }

    [JsonPropertyName("co2Saved")]
    public decimal Co2Ahorrado { get; set; }

    /// <summary>
    /// Porcentaje 0–100: activos / total (driver+passenger).
    /// </summary>
    [JsonPropertyName("adoptionRate")]
    public decimal TasaAdopcion { get; set; }

    /// <summary>
    /// Documenta la fórmula: "active_users_over_total".
    /// </summary>
    [JsonPropertyName("adoptionRateBasis")]
    public string BaseTasaAdopcion { get; set; } = "active_users_over_total";

    [JsonPropertyName("activeSos")]
    public IReadOnlyList<AlertaSosAdminDto> SosActivos { get; set; } = Array.Empty<AlertaSosAdminDto>();

    [JsonPropertyName("xpByCareer")]
    public IReadOnlyList<XpPorCarreraDto> XpPorCarrera { get; set; } = Array.Empty<XpPorCarreraDto>();

    [JsonPropertyName("gamificationEnabled")]
    public bool GamificacionHabilitada { get; set; }

    /// <summary>
    /// Nota cuando gamificación está off; null si está on.
    /// </summary>
    [JsonPropertyName("xpByCareerNote")]
    public string? NotaXpPorCarrera { get; set; }
}

public sealed class XpPorCarreraDto
{
    [JsonPropertyName("career")]
    public string Carrera { get; set; } = string.Empty;

    [JsonPropertyName("xp")]
    public int Xp { get; set; }
}

public sealed class KpisReporteDto
{
    [JsonPropertyName("trips")]
    public int Viajes { get; set; }

    [JsonPropertyName("km")]
    public decimal Km { get; set; }

    [JsonPropertyName("co2Saved")]
    public decimal Co2Ahorrado { get; set; }

    [JsonPropertyName("blockedUsers")]
    public int UsuariosBloqueados { get; set; }

    [JsonPropertyName("adoptionRate")]
    public decimal TasaAdopcion { get; set; }
}

public sealed class ViajeReporteDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("originText")]
    public string OrigenTexto { get; set; } = string.Empty;

    [JsonPropertyName("departureAt")]
    public DateTimeOffset SaleEn { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletadoEn { get; set; }

    [JsonPropertyName("distanceKm")]
    public decimal DistanciaKm { get; set; }

    [JsonPropertyName("co2SavedKg")]
    public decimal Co2AhorradoKg { get; set; }

    [JsonPropertyName("driverName")]
    public string NombreConductor { get; set; } = string.Empty;
}

public sealed class PuntoChartSemanalDto
{
    [JsonPropertyName("label")]
    public string Etiqueta { get; set; } = string.Empty;

    [JsonPropertyName("trips")]
    public int Viajes { get; set; }

    [JsonPropertyName("km")]
    public decimal Km { get; set; }
}

public sealed class ReporteAdminDto
{
    [JsonPropertyName("period")]
    public string Periodo { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public DateTimeOffset Desde { get; set; }

    [JsonPropertyName("to")]
    public DateTimeOffset Hasta { get; set; }

    [JsonPropertyName("kpis")]
    public KpisReporteDto Kpis { get; set; } = new();

    [JsonPropertyName("trips")]
    public IReadOnlyList<ViajeReporteDto> Viajes { get; set; } = Array.Empty<ViajeReporteDto>();

    [JsonPropertyName("weeklyChart")]
    public IReadOnlyList<PuntoChartSemanalDto> ChartSemanal { get; set; } = Array.Empty<PuntoChartSemanalDto>();
}

public sealed class ArchivoExportacion
{
    public byte[] Contenido { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
    public string NombreArchivo { get; set; } = "export.bin";
}

public sealed class EventoAuditoriaDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("action")]
    public string Accion { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severidad { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public Guid? UsuarioId { get; set; }

    [JsonPropertyName("userName")]
    public string? NombreUsuario { get; set; }

    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    [JsonPropertyName("device")]
    public string? Dispositivo { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreadoEn { get; set; }
}

public sealed class PaginaAuditoriaDto
{
    [JsonPropertyName("items")]
    public IReadOnlyList<EventoAuditoriaDto> Items { get; set; } = Array.Empty<EventoAuditoriaDto>();

    [JsonPropertyName("totalCount")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Pagina { get; set; }

    [JsonPropertyName("pageSize")]
    public int TamanoPagina { get; set; }
}

public sealed class NotificacionAdminDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Cuerpo { get; set; } = string.Empty;

    [JsonPropertyName("read")]
    public bool Leida { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreadoEn { get; set; }
}

public sealed class ConfiguracionUniversidadDto
{
    [JsonPropertyName("timezone")]
    public string ZonaHoraria { get; set; } = string.Empty;

    [JsonPropertyName("supportEmail")]
    public string CorreoSoporte { get; set; } = string.Empty;

    [JsonPropertyName("allowedEmailDomain")]
    public string? DominioCorreoPermitido { get; set; }

    [JsonPropertyName("maxDailyTrips")]
    public int MaxViajesDiarios { get; set; }

    [JsonPropertyName("minDriverRating")]
    public decimal CalificacionMinimaConductor { get; set; }

    [JsonPropertyName("co2FactorKgKm")]
    public decimal FactorCo2KgKm { get; set; }

    [JsonPropertyName("gamificationEnabled")]
    public bool GamificacionHabilitada { get; set; }

    [JsonPropertyName("co2TrackingEnabled")]
    public bool SeguimientoCo2Habilitado { get; set; }

    [JsonPropertyName("notifySos")]
    public bool NotificarSos { get; set; }

    [JsonPropertyName("notifyBlock")]
    public bool NotificarBloqueo { get; set; }

    [JsonPropertyName("notifyWeeklyReport")]
    public bool NotificarReporteSemanal { get; set; }
}

public sealed class SolicitudActualizarConfiguracion
{
    [JsonPropertyName("timezone")]
    public string? ZonaHoraria { get; set; }

    [JsonPropertyName("supportEmail")]
    public string? CorreoSoporte { get; set; }

    [JsonPropertyName("allowedEmailDomain")]
    public string? DominioCorreoPermitido { get; set; }

    [JsonPropertyName("maxDailyTrips")]
    public int? MaxViajesDiarios { get; set; }

    [JsonPropertyName("minDriverRating")]
    public decimal? CalificacionMinimaConductor { get; set; }

    [JsonPropertyName("co2FactorKgKm")]
    public decimal? FactorCo2KgKm { get; set; }

    [JsonPropertyName("gamificationEnabled")]
    public bool? GamificacionHabilitada { get; set; }

    [JsonPropertyName("co2TrackingEnabled")]
    public bool? SeguimientoCo2Habilitado { get; set; }

    [JsonPropertyName("notifySos")]
    public bool? NotificarSos { get; set; }

    [JsonPropertyName("notifyBlock")]
    public bool? NotificarBloqueo { get; set; }

    [JsonPropertyName("notifyWeeklyReport")]
    public bool? NotificarReporteSemanal { get; set; }
}
