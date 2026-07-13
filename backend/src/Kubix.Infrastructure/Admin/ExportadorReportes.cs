using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Kubix.Application.Admin;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Kubix.Infrastructure.Admin;

public static class ExportadorReportes
{
    private static int _licenciaConfigurada;

    private static void AsegurarLicencia()
    {
        if (Interlocked.Exchange(ref _licenciaConfigurada, 1) == 0)
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }
    }

    public static ArchivoExportacion Generar(ReporteAdminDto reporte, string formato)
    {
        AsegurarLicencia();
        var fmt = formato.Trim().ToLowerInvariant();
        return fmt switch
        {
            "csv" => GenerarCsv(reporte),
            "xlsx" => GenerarXlsx(reporte),
            "pdf" => GenerarPdf(reporte),
            _ => throw ExcepcionAdminOps.Validacion(
                "format must be csv, xlsx or pdf.",
                "invalid_format")
        };
    }

    private static ArchivoExportacion GenerarCsv(ReporteAdminDto reporte)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,status,originText,departureAt,completedAt,distanceKm,co2SavedKg,driverName");
        foreach (var v in reporte.Viajes)
        {
            sb.Append(EscaparCsv(v.Id.ToString())).Append(',');
            sb.Append(EscaparCsv(v.Estado)).Append(',');
            sb.Append(EscaparCsv(v.OrigenTexto)).Append(',');
            sb.Append(EscaparCsv(v.SaleEn.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscaparCsv(v.CompletadoEn?.ToString("o", CultureInfo.InvariantCulture) ?? "")).Append(',');
            sb.Append(EscaparCsv(v.DistanciaKm.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscaparCsv(v.Co2AhorradoKg.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.AppendLine(EscaparCsv(v.NombreConductor));
        }

        return new ArchivoExportacion
        {
            Contenido = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),
            ContentType = "text/csv; charset=utf-8",
            NombreArchivo = $"kubix_report_{reporte.Periodo}.csv"
        };
    }

    private static ArchivoExportacion GenerarXlsx(ReporteAdminDto reporte)
    {
        using var workbook = new XLWorkbook();
        var hoja = workbook.Worksheets.Add("Viajes");
        hoja.Cell(1, 1).Value = "id";
        hoja.Cell(1, 2).Value = "status";
        hoja.Cell(1, 3).Value = "originText";
        hoja.Cell(1, 4).Value = "departureAt";
        hoja.Cell(1, 5).Value = "completedAt";
        hoja.Cell(1, 6).Value = "distanceKm";
        hoja.Cell(1, 7).Value = "co2SavedKg";
        hoja.Cell(1, 8).Value = "driverName";

        var fila = 2;
        foreach (var v in reporte.Viajes)
        {
            hoja.Cell(fila, 1).Value = v.Id.ToString();
            hoja.Cell(fila, 2).Value = v.Estado;
            hoja.Cell(fila, 3).Value = v.OrigenTexto;
            hoja.Cell(fila, 4).Value = v.SaleEn.ToString("o", CultureInfo.InvariantCulture);
            hoja.Cell(fila, 5).Value = v.CompletadoEn?.ToString("o", CultureInfo.InvariantCulture) ?? "";
            hoja.Cell(fila, 6).Value = (double)v.DistanciaKm;
            hoja.Cell(fila, 7).Value = (double)v.Co2AhorradoKg;
            hoja.Cell(fila, 8).Value = v.NombreConductor;
            fila++;
        }

        var kpis = workbook.Worksheets.Add("KPIs");
        kpis.Cell(1, 1).Value = "metric";
        kpis.Cell(1, 2).Value = "value";
        kpis.Cell(2, 1).Value = "period";
        kpis.Cell(2, 2).Value = reporte.Periodo;
        kpis.Cell(3, 1).Value = "trips";
        kpis.Cell(3, 2).Value = reporte.Kpis.Viajes;
        kpis.Cell(4, 1).Value = "km";
        kpis.Cell(4, 2).Value = (double)reporte.Kpis.Km;
        kpis.Cell(5, 1).Value = "co2Saved";
        kpis.Cell(5, 2).Value = (double)reporte.Kpis.Co2Ahorrado;
        kpis.Cell(6, 1).Value = "blockedUsers";
        kpis.Cell(6, 2).Value = reporte.Kpis.UsuariosBloqueados;
        kpis.Cell(7, 1).Value = "adoptionRate";
        kpis.Cell(7, 2).Value = (double)reporte.Kpis.TasaAdopcion;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return new ArchivoExportacion
        {
            Contenido = ms.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            NombreArchivo = $"kubix_report_{reporte.Periodo}.xlsx"
        };
    }

    private static ArchivoExportacion GenerarPdf(ReporteAdminDto reporte)
    {
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Text($"Kubix — Reporte {reporte.Periodo}")
                    .SemiBold().FontSize(16);
                page.Content().Column(col =>
                {
                    col.Item().Text(
                        $"Periodo: {reporte.Desde:u} → {reporte.Hasta:u}");
                    col.Item().PaddingTop(8).Text(
                        $"Viajes: {reporte.Kpis.Viajes} · Km: {reporte.Kpis.Km} · CO₂: {reporte.Kpis.Co2Ahorrado} kg · Adopción: {reporte.Kpis.TasaAdopcion}%");
                    col.Item().PaddingTop(12).Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });
                        tabla.Header(h =>
                        {
                            h.Cell().Text("Origen").SemiBold();
                            h.Cell().Text("Conductor").SemiBold();
                            h.Cell().Text("Km").SemiBold();
                            h.Cell().Text("Estado").SemiBold();
                        });
                        foreach (var v in reporte.Viajes)
                        {
                            tabla.Cell().Text(v.OrigenTexto);
                            tabla.Cell().Text(v.NombreConductor);
                            tabla.Cell().Text(v.DistanciaKm.ToString(CultureInfo.InvariantCulture));
                            tabla.Cell().Text(v.Estado);
                        }
                    });
                });
            });
        });

        var bytes = documento.GeneratePdf();
        return new ArchivoExportacion
        {
            Contenido = bytes,
            ContentType = "application/pdf",
            NombreArchivo = $"kubix_report_{reporte.Periodo}.pdf"
        };
    }

    private static string EscaparCsv(string valor)
    {
        if (valor.Contains('"') || valor.Contains(',') || valor.Contains('\n') || valor.Contains('\r'))
        {
            return $"\"{valor.Replace("\"", "\"\"")}\"";
        }

        return valor;
    }
}
