namespace Kubix.Domain;

/// <summary>Marcadores especiales en solicitudes (sin migración de schema).</summary>
public static class MarcadoresSolicitud
{
    /// <summary>HashContrasena usado para solicitudes de cambio de vehículo.</summary>
    public const string CambioVehiculo = "$vehicle_change$";

    /// <summary>HashContrasena usado al solicitar cambio de pasajero a conductor.</summary>
    public const string CambioRolConductor = "$role_change_driver$";

    /// <summary>HashContrasena usado para cambios de perfil sujetos a aprobación.</summary>
    public const string CambioPerfil = "$profile_change$";
}
