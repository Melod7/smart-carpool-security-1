using Kubix.Domain;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kubix.Infrastructure.Persistence;

public class ContextoApp(DbContextOptions<ContextoApp> options) : DbContext(options)
{
    public DbSet<Universidad> Universidades => Set<Universidad>();
    public DbSet<Campus> Sedes => Set<Campus>();
    public DbSet<ConfiguracionUniversidad> ConfiguracionesUniversidad => Set<ConfiguracionUniversidad>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<TokenRefresco> TokensRefresco => Set<TokenRefresco>();
    public DbSet<SolicitudRegistro> SolicitudesRegistro => Set<SolicitudRegistro>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<Viaje> Viajes => Set<Viaje>();
    public DbSet<SolicitudViaje> SolicitudesViaje => Set<SolicitudViaje>();
    public DbSet<Calificacion> Calificaciones => Set<Calificacion>();
    public DbSet<AlertaSos> AlertasSos => Set<AlertaSos>();
    public DbSet<PingUbicacion> PingsUbicacion => Set<PingUbicacion>();
    public DbSet<EventoAuditoria> EventosAuditoria => Set<EventoAuditoria>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<ContactoEmergencia> ContactosEmergencia => Set<ContactoEmergencia>();
    public DbSet<TransaccionEcoToken> TransaccionesEcoToken => Set<TransaccionEcoToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigurarUniversidad(modelBuilder);
        ConfigurarCampus(modelBuilder);
        ConfigurarConfiguracionUniversidad(modelBuilder);
        ConfigurarUsuario(modelBuilder);
        ConfigurarTokenRefresco(modelBuilder);
        ConfigurarSolicitudRegistro(modelBuilder);
        ConfigurarVehiculo(modelBuilder);
        ConfigurarViaje(modelBuilder);
        ConfigurarSolicitudViaje(modelBuilder);
        ConfigurarCalificacion(modelBuilder);
        ConfigurarAlertaSos(modelBuilder);
        ConfigurarPingUbicacion(modelBuilder);
        ConfigurarEventoAuditoria(modelBuilder);
        ConfigurarNotificacion(modelBuilder);
        ConfigurarContactoEmergencia(modelBuilder);
        ConfigurarTransaccionEcoToken(modelBuilder);
    }

    private static ValueConverter<TEnum, string> ConversorEnum<TEnum>()
        where TEnum : struct, Enum =>
        new(
            v => ConversorEnumDominio.ACadenaDb(v),
            v => ConversorEnumDominio.DesdeCadenaDb<TEnum>(v));

    private static void ConfigurarCamposAuditables<TEntidad>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntidad> e)
        where TEntidad : EntidadAuditable
    {
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.CreadoEn).HasColumnName("created_at");
        e.Property(x => x.ActualizadoEn).HasColumnName("updated_at");
    }

    private static void ConfigurarUniversidad(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Universidad>();
        e.ToTable("universities");
        e.HasKey(x => x.Id);
        ConfigurarCamposAuditables(e);
        e.Property(x => x.Nombre).HasColumnName("name").HasMaxLength(200).IsRequired();
        e.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.Estado).HasColumnName("status").HasConversion(ConversorEnum<EstadoUniversidad>()).HasMaxLength(32);
    }

    private static void ConfigurarCampus(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Campus>();
        e.ToTable("campuses");
        e.HasKey(x => x.Id);
        ConfigurarCamposAuditables(e);
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.Nombre).HasColumnName("name").HasMaxLength(200).IsRequired();
        e.Property(x => x.Direccion).HasColumnName("address").HasMaxLength(500).IsRequired();
        e.Property(x => x.Lat).HasColumnName("lat");
        e.Property(x => x.Lng).HasColumnName("lng");
        e.HasIndex(x => new { x.UniversidadId, x.Nombre }).IsUnique();
        e.HasOne(x => x.Universidad).WithMany(x => x.Sedes).HasForeignKey(x => x.UniversidadId);
    }

    private static void ConfigurarConfiguracionUniversidad(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<ConfiguracionUniversidad>();
        e.ToTable("university_settings");
        e.HasKey(x => x.UniversidadId);
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.DominioCorreoPermitido).HasColumnName("allowed_email_domain").HasMaxLength(200);
        e.Property(x => x.ZonaHoraria).HasColumnName("timezone").HasMaxLength(100).IsRequired();
        e.Property(x => x.CorreoSoporte).HasColumnName("support_email").HasMaxLength(200).IsRequired();
        e.Property(x => x.MaxViajesDiariosPorConductor).HasColumnName("max_daily_trips_per_driver");
        e.Property(x => x.CalificacionMinimaConductor).HasColumnName("min_driver_rating").HasPrecision(3, 2);
        e.Property(x => x.FactorCo2KgKm).HasColumnName("co2_factor_kg_km").HasPrecision(8, 4);
        e.Property(x => x.GamificacionHabilitada).HasColumnName("gamification_enabled");
        e.Property(x => x.SeguimientoCo2Habilitado).HasColumnName("co2_tracking_enabled");
        e.Property(x => x.NotificarSos).HasColumnName("notify_sos");
        e.Property(x => x.NotificarBloqueo).HasColumnName("notify_block");
        e.Property(x => x.NotificarReporteSemanal).HasColumnName("notify_weekly_report");
        e.Property(x => x.CreadoEn).HasColumnName("created_at");
        e.Property(x => x.ActualizadoEn).HasColumnName("updated_at");
        e.HasOne(x => x.Universidad).WithOne(x => x.Configuracion).HasForeignKey<ConfiguracionUniversidad>(x => x.UniversidadId);
    }

    private static void ConfigurarUsuario(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Usuario>();
        e.ToTable("users");
        e.HasKey(x => x.Id);
        ConfigurarCamposAuditables(e);
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.CampusId).HasColumnName("campus_id");
        e.Property(x => x.Rol).HasColumnName("role").HasConversion(ConversorEnum<RolUsuario>()).HasMaxLength(32);
        e.Property(x => x.Estado).HasColumnName("status").HasConversion(ConversorEnum<EstadoUsuario>()).HasMaxLength(32);
        e.Property(x => x.Nombre).HasColumnName("name").HasMaxLength(200).IsRequired();
        e.Property(x => x.Correo).HasColumnName("email").HasMaxLength(320).IsRequired();
        e.Property(x => x.HashContrasena).HasColumnName("password_hash").HasMaxLength(200).IsRequired();
        e.Property(x => x.Carrera).HasColumnName("career").HasMaxLength(200);
        e.Property(x => x.NumeroIdentificacion).HasColumnName("id_number").HasMaxLength(50);
        e.Property(x => x.PromedioCalificacion).HasColumnName("rating_avg").HasPrecision(3, 2);
        e.Property(x => x.BalanceEco).HasColumnName("eco_balance");
        e.Property(x => x.EcoVitalicio).HasColumnName("eco_lifetime");
        e.Property(x => x.DebeCambiarContrasena).HasColumnName("must_change_password");
        e.HasIndex(x => new { x.Correo, x.UniversidadId }).IsUnique();
        e.HasOne(x => x.Universidad).WithMany(x => x.Usuarios).HasForeignKey(x => x.UniversidadId);
        e.HasOne(x => x.Campus).WithMany().HasForeignKey(x => x.CampusId);
    }

    private static void ConfigurarTokenRefresco(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<TokenRefresco>();
        e.ToTable("refresh_tokens");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.UsuarioId).HasColumnName("user_id");
        e.Property(x => x.HashToken).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        e.Property(x => x.ExpiraEn).HasColumnName("expires_at");
        e.Property(x => x.RevocadoEn).HasColumnName("revoked_at");
        e.Property(x => x.CreadoEn).HasColumnName("created_at");
        e.HasIndex(x => x.HashToken).IsUnique();
        e.HasOne(x => x.Usuario).WithMany(x => x.TokensRefresco).HasForeignKey(x => x.UsuarioId);
    }

    private static void ConfigurarSolicitudRegistro(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<SolicitudRegistro>();
        e.ToTable("registration_requests");
        e.HasKey(x => x.Id);
        ConfigurarCamposAuditables(e);
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.CampusId).HasColumnName("campus_id");
        e.Property(x => x.Nombre).HasColumnName("name").HasMaxLength(200).IsRequired();
        e.Property(x => x.Correo).HasColumnName("email").HasMaxLength(320).IsRequired();
        e.Property(x => x.HashContrasena).HasColumnName("password_hash").HasMaxLength(200).IsRequired();
        e.Property(x => x.Rol).HasColumnName("role").HasConversion(ConversorEnum<RolUsuario>()).HasMaxLength(32);
        e.Property(x => x.Carrera).HasColumnName("career").HasMaxLength(200);
        e.Property(x => x.NumeroIdentificacion).HasColumnName("id_number").HasMaxLength(50);
        e.Property(x => x.VehiculoJson).HasColumnName("vehicle_json");
        e.Property(x => x.Estado).HasColumnName("status").HasConversion(ConversorEnum<EstadoSolicitudRegistro>()).HasMaxLength(32);
        e.Property(x => x.DecididoPor).HasColumnName("decided_by");
        e.Property(x => x.DecididoEn).HasColumnName("decided_at");
        e.HasOne(x => x.Universidad).WithMany().HasForeignKey(x => x.UniversidadId);
        e.HasOne(x => x.Campus).WithMany().HasForeignKey(x => x.CampusId);
    }

    private static void ConfigurarVehiculo(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Vehiculo>();
        e.ToTable("vehicles");
        e.HasKey(x => x.Id);
        ConfigurarCamposAuditables(e);
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.UsuarioId).HasColumnName("user_id");
        e.Property(x => x.MarcaModelo).HasColumnName("make_model").HasMaxLength(200).IsRequired();
        e.Property(x => x.Placa).HasColumnName("plate").HasMaxLength(32).IsRequired();
        e.Property(x => x.Color).HasColumnName("color").HasMaxLength(64).IsRequired();
        e.Property(x => x.AsientosTotales).HasColumnName("seats_total");
        e.HasIndex(x => x.UsuarioId).IsUnique();
        e.HasOne(x => x.Universidad).WithMany().HasForeignKey(x => x.UniversidadId);
        e.HasOne(x => x.Usuario).WithMany(x => x.Vehiculos).HasForeignKey(x => x.UsuarioId);
    }

    private static void ConfigurarViaje(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Viaje>();
        e.ToTable("trips");
        e.HasKey(x => x.Id);
        ConfigurarCamposAuditables(e);
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.ConductorId).HasColumnName("driver_id");
        e.Property(x => x.CampusDestinoId).HasColumnName("destination_campus_id");
        e.Property(x => x.OrigenTexto).HasColumnName("origin_text").HasMaxLength(500).IsRequired();
        e.Property(x => x.OrigenLat).HasColumnName("origin_lat");
        e.Property(x => x.OrigenLng).HasColumnName("origin_lng");
        e.Property(x => x.SaleEn).HasColumnName("departure_at");
        e.Property(x => x.AsientosDisponibles).HasColumnName("seats_available");
        e.Property(x => x.Polilinea).HasColumnName("polyline").HasMaxLength(8000);
        e.Property(x => x.DistanciaKm).HasColumnName("distance_km").HasPrecision(10, 3);
        e.Property(x => x.Estado).HasColumnName("status").HasConversion(ConversorEnum<EstadoViaje>()).HasMaxLength(32);
        e.Property(x => x.IniciadoEn).HasColumnName("started_at");
        e.Property(x => x.CompletadoEn).HasColumnName("completed_at");
        e.Property(x => x.Co2AhorradoKg).HasColumnName("co2_saved_kg").HasPrecision(10, 3);
        e.HasIndex(x => new { x.UniversidadId, x.Estado, x.SaleEn });
        e.HasOne(x => x.Universidad).WithMany().HasForeignKey(x => x.UniversidadId);
        e.HasOne(x => x.Conductor).WithMany().HasForeignKey(x => x.ConductorId);
        e.HasOne(x => x.CampusDestino).WithMany().HasForeignKey(x => x.CampusDestinoId);
    }

    private static void ConfigurarSolicitudViaje(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<SolicitudViaje>();
        e.ToTable("ride_requests");
        e.HasKey(x => x.Id);
        ConfigurarCamposAuditables(e);
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.ViajeId).HasColumnName("trip_id");
        e.Property(x => x.PasajeroId).HasColumnName("passenger_id");
        e.Property(x => x.RecogidaTexto).HasColumnName("pickup_text").HasMaxLength(500).IsRequired();
        e.Property(x => x.RecogidaLat).HasColumnName("pickup_lat");
        e.Property(x => x.RecogidaLng).HasColumnName("pickup_lng");
        e.Property(x => x.Estado).HasColumnName("status").HasConversion(ConversorEnum<EstadoSolicitudViaje>()).HasMaxLength(40);
        e.HasIndex(x => new { x.ViajeId, x.PasajeroId }).IsUnique();
        e.HasOne(x => x.Universidad).WithMany().HasForeignKey(x => x.UniversidadId);
        e.HasOne(x => x.Viaje).WithMany(x => x.SolicitudesViaje).HasForeignKey(x => x.ViajeId);
        e.HasOne(x => x.Pasajero).WithMany().HasForeignKey(x => x.PasajeroId);
    }

    private static void ConfigurarCalificacion(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Calificacion>();
        e.ToTable("ratings");
        e.HasKey(x => x.Id);
        ConfigurarCamposAuditables(e);
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.ViajeId).HasColumnName("trip_id");
        e.Property(x => x.CalificadorId).HasColumnName("rater_id");
        e.Property(x => x.CalificadoId).HasColumnName("rated_id");
        e.Property(x => x.Estrellas).HasColumnName("stars");
        e.Property(x => x.Comentario).HasColumnName("comment").HasMaxLength(1000);
        e.HasIndex(x => new { x.ViajeId, x.CalificadorId, x.CalificadoId }).IsUnique();
        e.HasOne(x => x.Universidad).WithMany().HasForeignKey(x => x.UniversidadId);
        e.HasOne(x => x.Viaje).WithMany(x => x.Calificaciones).HasForeignKey(x => x.ViajeId);
        e.HasOne(x => x.Calificador).WithMany().HasForeignKey(x => x.CalificadorId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Calificado).WithMany().HasForeignKey(x => x.CalificadoId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurarAlertaSos(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<AlertaSos>();
        e.ToTable("sos_alerts");
        e.HasKey(x => x.Id);
        ConfigurarCamposAuditables(e);
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.ViajeId).HasColumnName("trip_id");
        e.Property(x => x.UsuarioId).HasColumnName("user_id");
        e.Property(x => x.Lat).HasColumnName("lat");
        e.Property(x => x.Lng).HasColumnName("lng");
        e.Property(x => x.DisparadaEn).HasColumnName("fired_at");
        e.Property(x => x.Estado).HasColumnName("status").HasConversion(ConversorEnum<EstadoAlertaSos>()).HasMaxLength(32);
        e.Property(x => x.ResueltaPor).HasColumnName("resolved_by");
        e.Property(x => x.ResueltaEn).HasColumnName("resolved_at");
        e.HasIndex(x => new { x.UniversidadId, x.Estado });
        e.HasOne(x => x.Universidad).WithMany().HasForeignKey(x => x.UniversidadId);
        e.HasOne(x => x.Viaje).WithMany().HasForeignKey(x => x.ViajeId);
        e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId);
    }

    private static void ConfigurarPingUbicacion(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<PingUbicacion>();
        e.ToTable("location_pings");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.ViajeId).HasColumnName("trip_id");
        e.Property(x => x.UsuarioId).HasColumnName("user_id");
        e.Property(x => x.Lat).HasColumnName("lat");
        e.Property(x => x.Lng).HasColumnName("lng");
        e.Property(x => x.RegistradoEn).HasColumnName("recorded_at");
        e.HasIndex(x => new { x.ViajeId, x.UsuarioId, x.RegistradoEn });
        e.HasOne(x => x.Universidad).WithMany().HasForeignKey(x => x.UniversidadId);
        e.HasOne(x => x.Viaje).WithMany(x => x.PingsUbicacion).HasForeignKey(x => x.ViajeId);
        e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId);
    }

    private static void ConfigurarEventoAuditoria(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<EventoAuditoria>();
        e.ToTable("audit_events");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.UsuarioId).HasColumnName("user_id");
        e.Property(x => x.Accion).HasColumnName("action").HasMaxLength(200).IsRequired();
        e.Property(x => x.Tipo).HasColumnName("type").HasConversion(ConversorEnum<TipoEventoAuditoria>()).HasMaxLength(32);
        e.Property(x => x.Severidad).HasColumnName("severity").HasConversion(ConversorEnum<SeveridadAuditoria>()).HasMaxLength(32);
        e.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(64);
        e.Property(x => x.Dispositivo).HasColumnName("device").HasMaxLength(200);
        e.Property(x => x.CreadoEn).HasColumnName("created_at");
        e.HasIndex(x => new { x.UniversidadId, x.Tipo, x.CreadoEn });
        e.HasOne(x => x.Universidad).WithMany().HasForeignKey(x => x.UniversidadId);
        e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId);
    }

    private static void ConfigurarNotificacion(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Notificacion>();
        e.ToTable("notifications");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.UsuarioDestinatarioId).HasColumnName("recipient_user_id");
        e.Property(x => x.RolDestinatario)
            .HasColumnName("recipient_role")
            .HasConversion(
                v => v.HasValue ? ConversorEnumDominio.ACadenaDb(v.Value) : null,
                v => v == null ? null : ConversorEnumDominio.DesdeCadenaDb<RolUsuario>(v))
            .HasMaxLength(32);
        e.Property(x => x.Tipo).HasColumnName("type").HasConversion(ConversorEnum<TipoNotificacion>()).HasMaxLength(32);
        e.Property(x => x.Titulo).HasColumnName("title").HasMaxLength(200).IsRequired();
        e.Property(x => x.Cuerpo).HasColumnName("body").HasMaxLength(2000).IsRequired();
        e.Property(x => x.Leida).HasColumnName("read");
        e.Property(x => x.CreadoEn).HasColumnName("created_at");
        e.HasIndex(x => new { x.UniversidadId, x.Leida, x.CreadoEn });
        e.HasOne(x => x.Universidad).WithMany().HasForeignKey(x => x.UniversidadId);
        e.HasOne(x => x.UsuarioDestinatario).WithMany().HasForeignKey(x => x.UsuarioDestinatarioId);
    }

    private static void ConfigurarContactoEmergencia(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<ContactoEmergencia>();
        e.ToTable("emergency_contacts");
        e.HasKey(x => x.Id);
        ConfigurarCamposAuditables(e);
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.UsuarioId).HasColumnName("user_id");
        e.Property(x => x.Nombre).HasColumnName("name").HasMaxLength(200).IsRequired();
        e.Property(x => x.Relacion).HasColumnName("relationship").HasMaxLength(100).IsRequired();
        e.Property(x => x.Telefono).HasColumnName("phone").HasMaxLength(40).IsRequired();
        e.HasOne(x => x.Universidad).WithMany().HasForeignKey(x => x.UniversidadId);
        e.HasOne(x => x.Usuario).WithMany(x => x.ContactosEmergencia).HasForeignKey(x => x.UsuarioId);
    }

    private static void ConfigurarTransaccionEcoToken(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<TransaccionEcoToken>();
        e.ToTable("eco_token_transactions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.UniversidadId).HasColumnName("university_id");
        e.Property(x => x.UsuarioId).HasColumnName("user_id");
        e.Property(x => x.Tipo).HasColumnName("type").HasConversion(ConversorEnum<TipoTransaccionEcoToken>()).HasMaxLength(64);
        e.Property(x => x.Monto).HasColumnName("amount");
        e.Property(x => x.IdFuente).HasColumnName("source_id").HasMaxLength(64).IsRequired();
        e.Property(x => x.CreadoEn).HasColumnName("created_at");
        e.HasIndex(x => new { x.UsuarioId, x.Tipo, x.IdFuente }).IsUnique();
        e.HasOne(x => x.Universidad).WithMany().HasForeignKey(x => x.UniversidadId);
        e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId);
    }
}
