using System.Globalization;
using Kubix.Application.EcoTokens;
using Kubix.Domain;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kubix.Infrastructure.EcoTokens;

public sealed class MotorEcoTokens(ContextoApp db) : IMotorEcoTokens, IServicioEcoTokens
{
    private const int PremioConductor = 8;
    private const int PremioPasajero = 4;
    private const int PremioCalificacion = 2;
    private const int PremioRacha = 10;
    private const int PenalizacionMaxima = 5;
    private const int CompletadosParaRacha = 5;

    public async Task AlCompletarViajeAsync(Guid viajeId, CancellationToken ct = default)
    {
        var viaje = await db.Viajes
            .AsNoTracking()
            .Include(v => v.SolicitudesViaje)
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct);

        if (viaje is null)
        {
            return;
        }

        var config = await ObtenerConfigAsync(viaje.UniversidadId, ct);
        if (config is null || !config.GamificacionHabilitada)
        {
            return;
        }

        var idFuente = viaje.Id.ToString();

        await IntentarAcreditarAsync(
            viaje.UniversidadId,
            viaje.ConductorId,
            TipoTransaccionEcoToken.ViajeCompletadoConductor,
            PremioConductor,
            idFuente,
            afectaVitalicio: true,
            ct);

        var pasajerosAceptados = viaje.SolicitudesViaje
            .Where(s => s.Estado == EstadoSolicitudViaje.Aceptada)
            .Select(s => s.PasajeroId)
            .Distinct()
            .ToList();

        foreach (var pasajeroId in pasajerosAceptados)
        {
            await IntentarAcreditarAsync(
                viaje.UniversidadId,
                pasajeroId,
                TipoTransaccionEcoToken.ViajeCompletadoPasajero,
                PremioPasajero,
                idFuente,
                afectaVitalicio: true,
                ct);
        }

        var candidatosRacha = new HashSet<Guid> { viaje.ConductorId };
        foreach (var pasajeroId in pasajerosAceptados)
        {
            candidatosRacha.Add(pasajeroId);
        }

        var zona = config.ZonaHoraria;
        foreach (var usuarioId in candidatosRacha)
        {
            await EvaluarRachaSemanalAsync(viaje.UniversidadId, usuarioId, zona, ct);
        }
    }

    public async Task AlCancelacionTardiaAsync(
        Guid viajeId,
        Guid conductorId,
        CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == conductorId, ct);
        if (usuario?.UniversidadId is null)
        {
            return;
        }

        var universidadId = usuario.UniversidadId.Value;
        var config = await ObtenerConfigAsync(universidadId, ct);
        if (config is null || !config.GamificacionHabilitada)
        {
            return;
        }

        var idFuente = viajeId.ToString();
        if (await ExisteTransaccionAsync(
                conductorId,
                TipoTransaccionEcoToken.PenalizacionCancelacionTardia,
                idFuente,
                ct))
        {
            return;
        }

        // Re-read balance after existence check.
        await db.Entry(usuario).ReloadAsync(ct);
        var penalizacion = Math.Min(PenalizacionMaxima, usuario.BalanceEco);
        if (penalizacion <= 0)
        {
            return;
        }

        await IntentarAcreditarAsync(
            universidadId,
            conductorId,
            TipoTransaccionEcoToken.PenalizacionCancelacionTardia,
            -penalizacion,
            idFuente,
            afectaVitalicio: false,
            ct);
    }

    public async Task AlCalificarAsync(Guid calificacionId, CancellationToken ct = default)
    {
        var calificacion = await db.Calificaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == calificacionId, ct);

        if (calificacion is null)
        {
            return;
        }

        var config = await ObtenerConfigAsync(calificacion.UniversidadId, ct);
        if (config is null || !config.GamificacionHabilitada)
        {
            return;
        }

        await IntentarAcreditarAsync(
            calificacion.UniversidadId,
            calificacion.CalificadorId,
            TipoTransaccionEcoToken.CalificacionEnviada,
            PremioCalificacion,
            calificacion.Id.ToString(),
            afectaVitalicio: true,
            ct);
    }

    public async Task<ResumenEcoDto> ObtenerResumenAsync(
        Guid usuarioId,
        int pagina = 1,
        int tamanoPagina = 20,
        CancellationToken ct = default)
    {
        if (pagina < 1)
        {
            pagina = 1;
        }

        if (tamanoPagina < 1)
        {
            tamanoPagina = 20;
        }

        if (tamanoPagina > 100)
        {
            tamanoPagina = 100;
        }

        var usuario = await db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionEcoTokens.NoEncontrado("User not found.");

        var gamificacion = true;
        if (usuario.UniversidadId is Guid universidadId)
        {
            var config = await ObtenerConfigAsync(universidadId, ct);
            gamificacion = config?.GamificacionHabilitada ?? true;
        }

        var (level, progress) = NivelesEcoTokens.Calcular(usuario.EcoVitalicio);

        var consulta = db.TransaccionesEcoToken.AsNoTracking()
            .Where(t => t.UsuarioId == usuarioId)
            .OrderByDescending(t => t.CreadoEn);

        var total = await consulta.CountAsync(ct);
        var filas = await consulta
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(ct);

        return new ResumenEcoDto
        {
            Balance = usuario.BalanceEco,
            Lifetime = usuario.EcoVitalicio,
            Level = level,
            Progress = progress,
            GamificationEnabled = gamificacion,
            Prizes = gamificacion ? CatalogoPremiosEco.Todos : [],
            TotalCount = total,
            Transactions = filas.Select(t => new TransaccionEcoDto
            {
                Id = t.Id,
                Type = ConversorEnumDominio.ACadenaDb(t.Tipo),
                Amount = t.Monto,
                SourceId = t.IdFuente,
                CreatedAt = t.CreadoEn
            }).ToList()
        };
    }

    public async Task<ResultadoCanjePremioDto> CanjearPremioAsync(
        Guid usuarioId,
        string codigoPremio,
        CancellationToken ct = default)
    {
        var premio = CatalogoPremiosEco.Buscar(codigoPremio)
            ?? throw ExcepcionEcoTokens.Validacion(
                "Unknown prize code.",
                "unknown_prize");

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionEcoTokens.NoEncontrado("User not found.");

        if (usuario.UniversidadId is null)
        {
            throw ExcepcionEcoTokens.Prohibido(
                "User has no university.",
                "missing_university");
        }

        var universidadId = usuario.UniversidadId.Value;
        var config = await ObtenerConfigAsync(universidadId, ct);
        if (config is null || !config.GamificacionHabilitada)
        {
            throw ExcepcionEcoTokens.Prohibido(
                "Gamification is disabled.",
                "gamification_disabled");
        }

        if (usuario.BalanceEco < premio.Costo)
        {
            throw ExcepcionEcoTokens.Validacion(
                "Insufficient EcoTokensUTN balance.",
                "insufficient_balance");
        }

        var idFuente = $"{premio.Codigo}:{Guid.NewGuid():N}";
        var acreditado = await IntentarAcreditarAsync(
            universidadId,
            usuarioId,
            TipoTransaccionEcoToken.CanjePremio,
            -premio.Costo,
            idFuente,
            afectaVitalicio: false,
            ct);

        if (!acreditado)
        {
            throw ExcepcionEcoTokens.Conflicto(
                "Could not complete prize redemption.",
                "redemption_failed");
        }

        await db.Entry(usuario).ReloadAsync(ct);

        return new ResultadoCanjePremioDto
        {
            CodigoPremio = premio.Codigo,
            NombrePremio = premio.Nombre,
            Costo = premio.Costo,
            Balance = usuario.BalanceEco,
            Mensaje =
                $"Canjeaste {premio.Nombre} por {premio.Costo} EcoTokensUTN. " +
                "Retíralo con el coordinador de tu campus."
        };
    }

    public async Task<IReadOnlyList<CanjeAdminDto>> ListarCanjesAdminAsync(
        int limite = 50,
        CancellationToken ct = default)
    {
        if (limite < 1)
        {
            limite = 50;
        }

        if (limite > 200)
        {
            limite = 200;
        }

        var filas = await db.TransaccionesEcoToken.AsNoTracking()
            .Where(t => t.Tipo == TipoTransaccionEcoToken.CanjePremio)
            .OrderByDescending(t => t.CreadoEn)
            .Take(limite)
            .ToListAsync(ct);

        if (filas.Count == 0)
        {
            return [];
        }

        var usuarioIds = filas.Select(t => t.UsuarioId).Distinct().ToList();
        var nombres = await db.Usuarios.AsNoTracking()
            .Where(u => usuarioIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);

        return filas.Select(t =>
        {
            var codigo = t.IdFuente.Split(':')[0];
            var premio = CatalogoPremiosEco.Buscar(codigo);
            nombres.TryGetValue(t.UsuarioId, out var nombreUsuario);
            return new CanjeAdminDto
            {
                Id = t.Id,
                UsuarioId = t.UsuarioId,
                NombreUsuario = string.IsNullOrWhiteSpace(nombreUsuario)
                    ? "Usuario"
                    : nombreUsuario,
                CodigoPremio = premio?.Codigo ?? codigo,
                NombrePremio = premio?.Nombre ?? codigo,
                Costo = Math.Abs(t.Monto),
                CreadoEn = t.CreadoEn
            };
        }).ToList();
    }

    private async Task EvaluarRachaSemanalAsync(
        Guid universidadId,
        Guid usuarioId,
        string zonaHoraria,
        CancellationToken ct)
    {
        var (inicioUtc, finUtc, claveSemana) = VentanaSemanaUniversidad(DateTimeOffset.UtcNow, zonaHoraria);

        if (await ExisteTransaccionAsync(
                usuarioId,
                TipoTransaccionEcoToken.RachaSemanal,
                claveSemana,
                ct))
        {
            return;
        }

        var completadosEnSemana = await db.TransaccionesEcoToken.CountAsync(
            t => t.UsuarioId == usuarioId
                 && t.CreadoEn >= inicioUtc
                 && t.CreadoEn < finUtc
                 && (t.Tipo == TipoTransaccionEcoToken.ViajeCompletadoConductor
                     || t.Tipo == TipoTransaccionEcoToken.ViajeCompletadoPasajero),
            ct);

        if (completadosEnSemana < CompletadosParaRacha)
        {
            return;
        }

        await IntentarAcreditarAsync(
            universidadId,
            usuarioId,
            TipoTransaccionEcoToken.RachaSemanal,
            PremioRacha,
            claveSemana,
            afectaVitalicio: true,
            ct);
    }

    /// <summary>
    /// Inserta ledger + actualiza balance/vitalicio. Devuelve false si ya existía (idempotente).
    /// </summary>
    private async Task<bool> IntentarAcreditarAsync(
        Guid universidadId,
        Guid usuarioId,
        TipoTransaccionEcoToken tipo,
        int monto,
        string idFuente,
        bool afectaVitalicio,
        CancellationToken ct)
    {
        if (await ExisteTransaccionAsync(usuarioId, tipo, idFuente, ct))
        {
            return false;
        }

        if (monto == 0)
        {
            return false;
        }

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is null)
        {
            return false;
        }

        db.TransaccionesEcoToken.Add(new TransaccionEcoToken
        {
            UniversidadId = universidadId,
            UsuarioId = usuarioId,
            Tipo = tipo,
            Monto = monto,
            IdFuente = idFuente,
            CreadoEn = DateTimeOffset.UtcNow
        });

        usuario.BalanceEco += monto;
        if (usuario.BalanceEco < 0)
        {
            usuario.BalanceEco = 0;
        }

        if (afectaVitalicio && monto > 0)
        {
            usuario.EcoVitalicio += monto;
        }

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Carrera concurrente: unique(user, type, source) — treat as no-op.
            foreach (var entry in db.ChangeTracker.Entries()
                         .Where(e => e.State is EntityState.Added or EntityState.Modified)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }

    private Task<bool> ExisteTransaccionAsync(
        Guid usuarioId,
        TipoTransaccionEcoToken tipo,
        string idFuente,
        CancellationToken ct) =>
        db.TransaccionesEcoToken.AnyAsync(
            t => t.UsuarioId == usuarioId && t.Tipo == tipo && t.IdFuente == idFuente,
            ct);

    private Task<ConfiguracionUniversidad?> ObtenerConfigAsync(Guid universidadId, CancellationToken ct) =>
        db.ConfiguracionesUniversidad.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UniversidadId == universidadId, ct);

    /// <summary>
    /// Semana lun–dom en timezone de la universidad; clave ISO year-week (p.ej. 2026-W28).
    /// </summary>
    public static (DateTimeOffset InicioUtc, DateTimeOffset FinUtc, string ClaveIso) VentanaSemanaUniversidad(
        DateTimeOffset ahoraUtc,
        string zonaHoraria)
    {
        var tz = ResolverZona(zonaHoraria);
        var local = TimeZoneInfo.ConvertTime(ahoraUtc, tz);
        var fechaLocal = DateOnly.FromDateTime(local.DateTime);
        var diasDesdeLunes = ((int)fechaLocal.DayOfWeek + 6) % 7;
        var lunes = fechaLocal.AddDays(-diasDesdeLunes);
        var siguienteLunes = lunes.AddDays(7);

        var inicioUtc = ConvertirLocalAUtc(lunes.ToDateTime(TimeOnly.MinValue), tz);
        var finUtc = ConvertirLocalAUtc(siguienteLunes.ToDateTime(TimeOnly.MinValue), tz);

        var referencia = lunes.ToDateTime(TimeOnly.MinValue);
        var anioIso = ISOWeek.GetYear(referencia);
        var semanaIso = ISOWeek.GetWeekOfYear(referencia);
        var clave = $"{anioIso}-W{semanaIso:D2}";

        return (
            new DateTimeOffset(inicioUtc, TimeSpan.Zero),
            new DateTimeOffset(finUtc, TimeSpan.Zero),
            clave);
    }

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
