using System.Text.Json.Serialization;

namespace Kubix.Application.SuperAdmin;

public sealed class UniversidadResumenDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("campusesCount")]
    public int CantidadCampuses { get; set; }

    [JsonPropertyName("usersCount")]
    public int CantidadUsuarios { get; set; }
}

public sealed class UniversidadDetalleDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("allowedEmailDomain")]
    public string? DominioCorreoPermitido { get; set; }
}

public sealed class SolicitudCrearUniversidad
{
    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("allowedEmailDomain")]
    public string? DominioCorreoPermitido { get; set; }
}

public sealed class SolicitudActualizarUniversidad
{
    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;
}

public sealed class CampusDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("universityId")]
    public Guid UniversidadId { get; set; }

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Direccion { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }
}

public sealed class SolicitudCrearCampus
{
    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Direccion { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }
}

public sealed class SolicitudActualizarCampus
{
    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Direccion { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }
}

public sealed class CoordinadorDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Correo { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("mustChangePassword")]
    public bool DebeCambiarContrasena { get; set; }

    [JsonPropertyName("universityId")]
    public Guid? UniversidadId { get; set; }
}

public sealed class CoordinadorCreadoDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Correo { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("mustChangePassword")]
    public bool DebeCambiarContrasena { get; set; }

    [JsonPropertyName("universityId")]
    public Guid? UniversidadId { get; set; }

    [JsonPropertyName("temporaryPassword")]
    public string ContrasenaTemporal { get; set; } = string.Empty;
}

public sealed class SolicitudCrearCoordinador
{
    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Correo { get; set; } = string.Empty;
}

public sealed class RespuestaResetContrasena
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("email")]
    public string Correo { get; set; } = string.Empty;

    [JsonPropertyName("temporaryPassword")]
    public string ContrasenaTemporal { get; set; } = string.Empty;

    [JsonPropertyName("mustChangePassword")]
    public bool DebeCambiarContrasena { get; set; }
}

public sealed class StatsSuperAdminDto
{
    [JsonPropertyName("universitiesCount")]
    public int CantidadUniversidades { get; set; }

    [JsonPropertyName("totalUsers")]
    public int TotalUsuarios { get; set; }

    [JsonPropertyName("driversCount")]
    public int CantidadConductores { get; set; }

    [JsonPropertyName("passengersCount")]
    public int CantidadPasajeros { get; set; }

    [JsonPropertyName("tripsToday")]
    public int ViajesHoy { get; set; }

    [JsonPropertyName("activeSosCount")]
    public int CantidadSosActivos { get; set; }
}
