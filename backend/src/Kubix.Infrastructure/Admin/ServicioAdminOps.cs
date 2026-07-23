using Kubix.Application.Admin;
using Kubix.Application.Sos;
using Kubix.Application.Tenancy;
using Kubix.Domain;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.EcoTokens;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kubix.Infrastructure.Admin;

public sealed class ServicioAdminOps(ContextoApp db, IContextoInquilino inquilino) : IServicioAdminOps
{
    private const string NotaXpDeshabilitado =
        "La gamificación está desactivada para esta universidad; no hay EcoTokensUTN por carrera.";

    private static readonly string[] DiasCortosEs =
        ["Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb"];

    public async Task<DashboardAdminDto> ObtenerDashboardAsync(CancellationToken ct = default)
    {
        var universidadId = RequerirUniversidad();
        var config = await ObtenerConfigAsync(universidadId, ct);
        var zona = config.ZonaHoraria;
        var (inicioHoy, finHoy) = VentanaDiaUniversidad(DateTimeOffset.UtcNow, zona);

        var viajesHoy = await db.Viajes.CountAsync(
            v => v.SaleEn >= inicioHoy && v.SaleEn < finHoy,
            ct);

        var bloqueados = await db.Usuarios.CountAsync(
            u => u.Estado == EstadoUsuario.Bloqueado
                 && (u.Rol == RolUsuario.Conductor || u.Rol == RolUsuario.Pasajero),
            ct);

        var co2 = await db.Viajes
            .Where(v => v.Estado == EstadoViaje.Completado)
            .SumAsync(v => (decimal?)v.Co2AhorradoKg, ct) ?? 0m;

        var tasa = await CalcularTasaAdopcionAsync(ct);

        var sos = await ListarSosActivosAsync(ct);

        var gamificacion = config.GamificacionHabilitada;
        IReadOnlyList<XpPorCarreraDto> xp;
        string? notaXp = null;

        if (!gamificacion)
        {
            xp = Array.Empty<XpPorCarreraDto>();
            notaXp = NotaXpDeshabilitado;
        }
        else
        {
            xp = await ObtenerXpPorCarreraAsync(universidadId, zona, ct);
        }

        return new DashboardAdminDto
        {
            ViajesHoy = viajesHoy,
            UsuariosBloqueados = bloqueados,
            Co2Ahorrado = decimal.Round(co2, 3),
            TasaAdopcion = tasa,
            BaseTasaAdopcion = "active_users_over_total",
            SosActivos = sos,
            XpPorCarrera = xp,
            GamificacionHabilitada = gamificacion,
            NotaXpPorCarrera = notaXp
        };
    }

    public async Task<ReporteAdminDto> ObtenerReporteAsync(string periodo, CancellationToken ct = default)
    {
        var universidadId = RequerirUniversidad();
        var config = await ObtenerConfigAsync(universidadId, ct);
        var (desde, hasta, periodoNorm) = ResolverPeriodo(periodo, DateTimeOffset.UtcNow, config.ZonaHoraria);

        var viajes = await db.Viajes
            .AsNoTracking()
            .Include(v => v.Conductor)
            .Where(v => v.SaleEn >= desde && v.SaleEn < hasta)
            .OrderByDescending(v => v.SaleEn)
            .ToListAsync(ct);

        var bloqueados = await db.Usuarios.CountAsync(
            u => u.Estado == EstadoUsuario.Bloqueado
                 && (u.Rol == RolUsuario.Conductor || u.Rol == RolUsuario.Pasajero),
            ct);

        var tasa = await CalcularTasaAdopcionAsync(ct);
        var km = viajes.Sum(v => v.DistanciaKm);
        var co2 = viajes.Where(v => v.Estado == EstadoViaje.Completado).Sum(v => v.Co2AhorradoKg);

        var chart = ConstruirChartSemanal(viajes, desde, hasta, config.ZonaHoraria);

        return new ReporteAdminDto
        {
            Periodo = periodoNorm,
            Desde = desde,
            Hasta = hasta,
            Kpis = new KpisReporteDto
            {
                Viajes = viajes.Count,
                Km = decimal.Round(km, 2),
                Co2Ahorrado = decimal.Round(co2, 3),
                UsuariosBloqueados = bloqueados,
                TasaAdopcion = tasa
            },
            Viajes = viajes.Select(MapearViajeReporte).ToList(),
            ChartSemanal = chart
        };
    }

    public async Task<ArchivoExportacion> ExportarReporteAsync(
        string periodo,
        string formato,
        CancellationToken ct = default)
    {
        try
        {
            var reporte = await ObtenerReporteAsync(periodo, ct);
            return ExportadorReportes.Generar(reporte, formato);
        }
        catch (ExcepcionAdminOps)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ExcepcionAdminOps.ErrorInterno(
                $"Failed to generate report export: {ex.Message}",
                "export_failed");
        }
    }

    public async Task<PaginaAuditoriaDto> ListarAuditoriaAsync(
        string? tipo,
        string? severidad,
        int pagina,
        int tamanoPagina,
        CancellationToken ct = default)
    {
        RequerirUniversidad();
        if (pagina < 1) pagina = 1;
        if (tamanoPagina is < 1 or > 100) tamanoPagina = 20;

        var query = AplicarFiltrosAuditoria(
            db.EventosAuditoria.AsNoTracking(),
            tipo,
            severidad);

        var total = await query.CountAsync(ct);
        var filas = await query
            .Include(e => e.Usuario)
            .OrderByDescending(e => e.CreadoEn)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(ct);

        var items = filas.Select(MapearEventoAuditoria).ToList();

        return new PaginaAuditoriaDto
        {
            Items = items,
            Total = total,
            Pagina = pagina,
            TamanoPagina = tamanoPagina
        };
    }

    public async Task<ArchivoExportacion> ExportarAuditoriaPdfAsync(
        string? tipo,
        string? severidad,
        CancellationToken ct = default)
    {
        RequerirUniversidad();

        try
        {
            var filas = await AplicarFiltrosAuditoria(
                    db.EventosAuditoria.AsNoTracking(),
                    tipo,
                    severidad)
                .Include(e => e.Usuario)
                .OrderByDescending(e => e.CreadoEn)
                .ToListAsync(ct);

            return ExportadorAuditoria.GenerarPdf(
                filas.Select(MapearEventoAuditoria).ToList());
        }
        catch (ExcepcionAdminOps)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ExcepcionAdminOps.ErrorInterno(
                $"Failed to generate audit PDF: {ex.Message}",
                "audit_export_failed");
        }
    }

    public async Task<IReadOnlyList<NotificacionAdminDto>> ListarNotificacionesAsync(
        Guid usuarioId,
        CancellationToken ct = default)
    {
        RequerirUniversidad();

        var lista = await db.Notificaciones
            .AsNoTracking()
            .Where(n =>
                n.RolDestinatario == RolUsuario.Coordinador
                || n.UsuarioDestinatarioId == usuarioId)
            .OrderByDescending(n => n.CreadoEn)
            .Take(100)
            .ToListAsync(ct);

        return lista.Select(n => new NotificacionAdminDto
        {
            Id = n.Id,
            Tipo = ConversorEnumDominio.ACadenaDb(n.Tipo),
            Titulo = n.Titulo,
            Cuerpo = n.Cuerpo,
            Leida = n.Leida,
            CreadoEn = n.CreadoEn
        }).ToList();
    }

    public async Task MarcarNotificacionLeidaAsync(
        Guid usuarioId,
        Guid notificacionId,
        CancellationToken ct = default)
    {
        RequerirUniversidad();

        var n = await db.Notificaciones.FirstOrDefaultAsync(
            x => x.Id == notificacionId
                 && (x.RolDestinatario == RolUsuario.Coordinador
                     || x.UsuarioDestinatarioId == usuarioId),
            ct);

        if (n is null)
        {
            throw ExcepcionAdminOps.NoEncontrado("Notification not found.", "notification_not_found");
        }

        if (!n.Leida)
        {
            n.Leida = true;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task MarcarTodasNotificacionesLeidasAsync(Guid usuarioId, CancellationToken ct = default)
    {
        RequerirUniversidad();

        var pendientes = await db.Notificaciones
            .Where(n =>
                !n.Leida
                && (n.RolDestinatario == RolUsuario.Coordinador
                    || n.UsuarioDestinatarioId == usuarioId))
            .ToListAsync(ct);

        foreach (var n in pendientes)
        {
            n.Leida = true;
        }

        if (pendientes.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<ConfiguracionUniversidadDto> ObtenerConfiguracionAsync(CancellationToken ct = default)
    {
        var universidadId = RequerirUniversidad();
        var config = await ObtenerConfigAsync(universidadId, ct);
        return MapearConfig(config);
    }

    public async Task<ConfiguracionUniversidadDto> ActualizarConfiguracionAsync(
        SolicitudActualizarConfiguracion solicitud,
        CancellationToken ct = default)
    {
        var universidadId = RequerirUniversidad();
        var config = await db.ConfiguracionesUniversidad
            .FirstOrDefaultAsync(c => c.UniversidadId == universidadId, ct)
            ?? throw ExcepcionAdminOps.NoEncontrado("University settings not found.", "settings_not_found");

        if (solicitud.ZonaHoraria is not null)
        {
            if (string.IsNullOrWhiteSpace(solicitud.ZonaHoraria))
            {
                throw ExcepcionAdminOps.Validacion("timezone is required when provided.");
            }

            config.ZonaHoraria = solicitud.ZonaHoraria.Trim();
        }

        if (solicitud.CorreoSoporte is not null)
        {
            if (string.IsNullOrWhiteSpace(solicitud.CorreoSoporte) || !solicitud.CorreoSoporte.Contains('@'))
            {
                throw ExcepcionAdminOps.Validacion("supportEmail must be a valid email.");
            }

            config.CorreoSoporte = solicitud.CorreoSoporte.Trim();
        }

        if (solicitud.DominioCorreoPermitido is not null)
        {
            config.DominioCorreoPermitido = string.IsNullOrWhiteSpace(solicitud.DominioCorreoPermitido)
                ? null
                : solicitud.DominioCorreoPermitido.Trim().ToLowerInvariant();
        }

        if (solicitud.MaxViajesDiarios is not null)
        {
            if (solicitud.MaxViajesDiarios is < 1 or > 50)
            {
                throw ExcepcionAdminOps.Validacion("maxDailyTrips must be between 1 and 50.");
            }

            config.MaxViajesDiariosPorConductor = solicitud.MaxViajesDiarios.Value;
        }

        if (solicitud.CalificacionMinimaConductor is not null)
        {
            if (solicitud.CalificacionMinimaConductor is < 0 or > 5)
            {
                throw ExcepcionAdminOps.Validacion("minDriverRating must be between 0 and 5.");
            }

            config.CalificacionMinimaConductor = solicitud.CalificacionMinimaConductor.Value;
        }

        if (solicitud.FactorCo2KgKm is not null)
        {
            if (solicitud.FactorCo2KgKm < 0)
            {
                throw ExcepcionAdminOps.Validacion("co2FactorKgKm must be >= 0.");
            }

            config.FactorCo2KgKm = solicitud.FactorCo2KgKm.Value;
        }

        if (solicitud.GamificacionHabilitada is not null)
        {
            config.GamificacionHabilitada = solicitud.GamificacionHabilitada.Value;
        }

        if (solicitud.SeguimientoCo2Habilitado is not null)
        {
            config.SeguimientoCo2Habilitado = solicitud.SeguimientoCo2Habilitado.Value;
        }

        if (solicitud.NotificarSos is not null)
        {
            config.NotificarSos = solicitud.NotificarSos.Value;
        }

        if (solicitud.NotificarBloqueo is not null)
        {
            config.NotificarBloqueo = solicitud.NotificarBloqueo.Value;
        }

        if (solicitud.NotificarReporteSemanal is not null)
        {
            config.NotificarReporteSemanal = solicitud.NotificarReporteSemanal.Value;
        }

        config.ActualizadoEn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return MapearConfig(config);
    }

    private async Task<IReadOnlyList<XpPorCarreraDto>> ObtenerXpPorCarreraAsync(
        Guid universidadId,
        string zona,
        CancellationToken ct)
    {
        var (inicioSemana, finSemana, _) = MotorEcoTokens.VentanaSemanaUniversidad(
            DateTimeOffset.UtcNow,
            zona);

        // Materializar primero: InMemory no traduce bien group-by con join + coalesce.
        var txs = await db.TransaccionesEcoToken
            .AsNoTracking()
            .Where(t =>
                t.UniversidadId == universidadId
                && t.Monto > 0
                && t.CreadoEn >= inicioSemana
                && t.CreadoEn < finSemana)
            .Select(t => new { t.UsuarioId, t.Monto })
            .ToListAsync(ct);

        if (txs.Count == 0)
        {
            return Array.Empty<XpPorCarreraDto>();
        }

        var usuarioIds = txs.Select(t => t.UsuarioId).Distinct().ToList();
        var carreras = await db.Usuarios
            .AsNoTracking()
            .Where(u => usuarioIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Carrera })
            .ToDictionaryAsync(u => u.Id, u => u.Carrera ?? "Sin carrera", ct);

        return txs
            .GroupBy(t => carreras.GetValueOrDefault(t.UsuarioId, "Sin carrera"))
            .Select(g => new XpPorCarreraDto
            {
                Carrera = g.Key,
                Xp = g.Sum(x => x.Monto)
            })
            .OrderByDescending(x => x.Xp)
            .ToList();
    }

    private async Task<IReadOnlyList<AlertaSosAdminDto>> ListarSosActivosAsync(CancellationToken ct)
    {
        var alertas = await db.AlertasSos
            .AsNoTracking()
            .Include(a => a.Usuario)
            .Include(a => a.Viaje)!.ThenInclude(v => v!.Conductor)
            .Where(a => a.Estado == EstadoAlertaSos.Activa)
            .OrderByDescending(a => a.DisparadaEn)
            .ToListAsync(ct);

        return alertas.Select(MapearSosAdmin).ToList();
    }

    private async Task<decimal> CalcularTasaAdopcionAsync(CancellationToken ct)
    {
        var total = await db.Usuarios.CountAsync(
            u => (u.Rol == RolUsuario.Conductor || u.Rol == RolUsuario.Pasajero)
                 && u.Estado != EstadoUsuario.Eliminado,
            ct);

        if (total == 0)
        {
            return 0m;
        }

        var activos = await db.Usuarios.CountAsync(
            u => (u.Rol == RolUsuario.Conductor || u.Rol == RolUsuario.Pasajero)
                 && u.Estado == EstadoUsuario.Activo,
            ct);

        return decimal.Round(100m * activos / total, 1);
    }

    private async Task<ConfiguracionUniversidad> ObtenerConfigAsync(Guid universidadId, CancellationToken ct) =>
        await db.ConfiguracionesUniversidad.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UniversidadId == universidadId, ct)
        ?? throw ExcepcionAdminOps.NoEncontrado("University settings not found.", "settings_not_found");

    private static IQueryable<EventoAuditoria> AplicarFiltrosAuditoria(
        IQueryable<EventoAuditoria> query,
        string? tipo,
        string? severidad)
    {
        if (!string.IsNullOrWhiteSpace(tipo))
        {
            try
            {
                var tipoEnum = ConversorEnumDominio.DesdeCadenaDb<TipoEventoAuditoria>(
                    tipo.Trim().ToLowerInvariant());
                query = query.Where(e => e.Tipo == tipoEnum);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw ExcepcionAdminOps.Validacion("Invalid type filter.", "invalid_filter");
            }
        }

        if (!string.IsNullOrWhiteSpace(severidad))
        {
            try
            {
                var severidadEnum = ConversorEnumDominio.DesdeCadenaDb<SeveridadAuditoria>(
                    severidad.Trim().ToLowerInvariant());
                query = query.Where(e => e.Severidad == severidadEnum);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw ExcepcionAdminOps.Validacion("Invalid severity filter.", "invalid_filter");
            }
        }

        return query;
    }

    private static EventoAuditoriaDto MapearEventoAuditoria(EventoAuditoria evento) =>
        new()
        {
            Id = evento.Id,
            Accion = evento.Accion,
            Tipo = ConversorEnumDominio.ACadenaDb(evento.Tipo),
            Severidad = ConversorEnumDominio.ACadenaDb(evento.Severidad),
            UsuarioId = evento.UsuarioId,
            NombreUsuario = evento.Usuario?.Nombre,
            Ip = evento.Ip,
            Dispositivo = evento.Dispositivo,
            CreadoEn = evento.CreadoEn
        };

    private Guid RequerirUniversidad()
    {
        if (inquilino.UniversidadId is null || inquilino.UniversidadId == Guid.Empty)
        {
            throw ExcepcionAdminOps.Prohibido("University context required.");
        }

        return inquilino.UniversidadId.Value;
    }

    private static ConfiguracionUniversidadDto MapearConfig(ConfiguracionUniversidad c) =>
        new()
        {
            ZonaHoraria = c.ZonaHoraria,
            CorreoSoporte = c.CorreoSoporte,
            DominioCorreoPermitido = c.DominioCorreoPermitido,
            MaxViajesDiarios = c.MaxViajesDiariosPorConductor,
            CalificacionMinimaConductor = c.CalificacionMinimaConductor,
            FactorCo2KgKm = c.FactorCo2KgKm,
            GamificacionHabilitada = c.GamificacionHabilitada,
            SeguimientoCo2Habilitado = c.SeguimientoCo2Habilitado,
            NotificarSos = c.NotificarSos,
            NotificarBloqueo = c.NotificarBloqueo,
            NotificarReporteSemanal = c.NotificarReporteSemanal
        };

    private static ViajeReporteDto MapearViajeReporte(Viaje v) =>
        new()
        {
            Id = v.Id,
            Estado = ConversorEnumDominio.ACadenaDb(v.Estado),
            OrigenTexto = v.OrigenTexto,
            SaleEn = v.SaleEn,
            CompletadoEn = v.CompletadoEn,
            DistanciaKm = v.DistanciaKm,
            Co2AhorradoKg = v.Co2AhorradoKg,
            NombreConductor = v.Conductor?.Nombre ?? ""
        };

    private static AlertaSosAdminDto MapearSosAdmin(AlertaSos a)
    {
        ParticipanteSosDto? conductor = null;
        ViajeSosDto? viaje = null;

        if (a.Viaje is not null)
        {
            viaje = new ViajeSosDto
            {
                Id = a.Viaje.Id,
                Estado = ConversorEnumDominio.ACadenaDb(a.Viaje.Estado),
                OrigenTexto = a.Viaje.OrigenTexto,
                SaleEn = a.Viaje.SaleEn
            };

            if (a.Viaje.Conductor is not null)
            {
                conductor = new ParticipanteSosDto
                {
                    Id = a.Viaje.Conductor.Id,
                    Nombre = a.Viaje.Conductor.Nombre,
                    Correo = a.Viaje.Conductor.Correo,
                    Rol = ConversorEnumDominio.ACadenaDb(a.Viaje.Conductor.Rol)
                };
            }
        }

        return new AlertaSosAdminDto
        {
            Id = a.Id,
            Estado = ConversorEnumDominio.ACadenaDb(a.Estado),
            Lat = a.Lat,
            Lng = a.Lng,
            DisparadaEn = a.DisparadaEn,
            ResueltaPor = a.ResueltaPor,
            ResueltaEn = a.ResueltaEn,
            Estudiante = new ParticipanteSosDto
            {
                Id = a.Usuario.Id,
                Nombre = a.Usuario.Nombre,
                Correo = a.Usuario.Correo,
                Rol = ConversorEnumDominio.ACadenaDb(a.Usuario.Rol)
            },
            Viaje = viaje,
            Conductor = conductor
        };
    }

    /// <summary>
    /// period=diario|semanal|mensual|trimestral|anual (español, por diseño).
    /// </summary>
    internal static (DateTimeOffset Desde, DateTimeOffset Hasta, string Periodo) ResolverPeriodo(
        string periodo,
        DateTimeOffset ahoraUtc,
        string zonaHoraria)
    {
        var p = (periodo ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(p))
        {
            p = "semanal";
        }

        var tz = ResolverZona(zonaHoraria);
        var local = TimeZoneInfo.ConvertTime(ahoraUtc, tz);
        var fecha = DateOnly.FromDateTime(local.DateTime);

        DateOnly inicioLocal;
        DateOnly finExclusivoLocal;

        switch (p)
        {
            case "diario":
                inicioLocal = fecha;
                finExclusivoLocal = fecha.AddDays(1);
                break;
            case "semanal":
            {
                var diasDesdeLunes = ((int)fecha.DayOfWeek + 6) % 7;
                inicioLocal = fecha.AddDays(-diasDesdeLunes);
                finExclusivoLocal = inicioLocal.AddDays(7);
                break;
            }
            case "mensual":
                inicioLocal = new DateOnly(fecha.Year, fecha.Month, 1);
                finExclusivoLocal = inicioLocal.AddMonths(1);
                break;
            case "trimestral":
            {
                var mesInicioTrimestre = ((fecha.Month - 1) / 3) * 3 + 1;
                inicioLocal = new DateOnly(fecha.Year, mesInicioTrimestre, 1);
                finExclusivoLocal = inicioLocal.AddMonths(3);
                break;
            }
            case "anual":
                inicioLocal = new DateOnly(fecha.Year, 1, 1);
                finExclusivoLocal = new DateOnly(fecha.Year + 1, 1, 1);
                break;
            default:
                throw ExcepcionAdminOps.Validacion(
                    "period must be diario|semanal|mensual|trimestral|anual.",
                    "invalid_period");
        }

        var desde = new DateTimeOffset(ConvertirLocalAUtc(inicioLocal.ToDateTime(TimeOnly.MinValue), tz), TimeSpan.Zero);
        var hasta = new DateTimeOffset(ConvertirLocalAUtc(finExclusivoLocal.ToDateTime(TimeOnly.MinValue), tz), TimeSpan.Zero);
        return (desde, hasta, p);
    }

    private static IReadOnlyList<PuntoChartSemanalDto> ConstruirChartSemanal(
        IReadOnlyList<Viaje> viajes,
        DateTimeOffset desde,
        DateTimeOffset hasta,
        string zonaHoraria)
    {
        var tz = ResolverZona(zonaHoraria);
        var puntos = new List<PuntoChartSemanalDto>();

        // Buckets semanales (lun–dom) dentro del periodo; si el periodo es ≤7 días, buckets diarios.
        var span = hasta - desde;
        if (span <= TimeSpan.FromDays(8))
        {
            for (var cursor = desde; cursor < hasta; cursor = cursor.AddDays(1))
            {
                var fin = cursor.AddDays(1);
                var delDia = viajes.Where(v => v.SaleEn >= cursor && v.SaleEn < fin).ToList();
                var local = TimeZoneInfo.ConvertTime(cursor, tz);
                puntos.Add(new PuntoChartSemanalDto
                {
                    Etiqueta = EtiquetaDiaEs(local),
                    Viajes = delDia.Count,
                    Km = decimal.Round(delDia.Sum(v => v.DistanciaKm), 2)
                });
            }
        }
        else
        {
            var cursor = desde;
            while (cursor < hasta)
            {
                var fin = cursor.AddDays(7);
                if (fin > hasta) fin = hasta;
                var delBucket = viajes.Where(v => v.SaleEn >= cursor && v.SaleEn < fin).ToList();
                var local = TimeZoneInfo.ConvertTime(cursor, tz);
                puntos.Add(new PuntoChartSemanalDto
                {
                    Etiqueta = $"Sem {ISOWeek.GetWeekOfYear(local.DateTime)} {local:yyyy}",
                    Viajes = delBucket.Count,
                    Km = decimal.Round(delBucket.Sum(v => v.DistanciaKm), 2)
                });
                cursor = fin;
            }
        }

        return puntos;
    }

    internal static (DateTimeOffset Inicio, DateTimeOffset Fin) VentanaDiaUniversidad(
        DateTimeOffset ahoraUtc,
        string zonaHoraria)
    {
        var tz = ResolverZona(zonaHoraria);
        var local = TimeZoneInfo.ConvertTime(ahoraUtc, tz);
        var fecha = DateOnly.FromDateTime(local.DateTime);
        var inicioUtc = ConvertirLocalAUtc(fecha.ToDateTime(TimeOnly.MinValue), tz);
        var finUtc = ConvertirLocalAUtc(fecha.AddDays(1).ToDateTime(TimeOnly.MinValue), tz);
        return (
            new DateTimeOffset(inicioUtc, TimeSpan.Zero),
            new DateTimeOffset(finUtc, TimeSpan.Zero));
    }

    private static string EtiquetaDiaEs(DateTimeOffset local) =>
        $"{DiasCortosEs[(int)local.DayOfWeek]} {local:dd/MM}";

    private static DateTime ConvertirLocalAUtc(DateTime localUnspecified, TimeZoneInfo tz)
    {
        var local = DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    private static TimeZoneInfo ResolverZona(string zonaHoraria)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(zonaHoraria);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}

file static class ISOWeek
{
    public static int GetWeekOfYear(DateTime time) =>
        System.Globalization.ISOWeek.GetWeekOfYear(time);
}
