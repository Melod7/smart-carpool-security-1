using Kubix.Application.Admin;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace Kubix.Infrastructure.Admin;

public static class ExportadorAuditoria
{
    public static ArchivoExportacion GenerarPdf(IReadOnlyList<EventoAuditoriaDto> eventos)
    {
        ExportadorReportes.AsegurarLicencia();

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(style => style.FontSize(8));

                page.Header().Column(header =>
                {
                    header.Item().Text("Kubix — Auditoría de seguridad")
                        .SemiBold()
                        .FontSize(16);
                    header.Item().Text(
                        $"Generado: {DateTimeOffset.UtcNow:dd/MM/yyyy HH:mm} UTC · Eventos: {eventos.Count}");
                });

                page.Content().PaddingTop(12).Table(tabla =>
                {
                    tabla.ColumnsDefinition(columnas =>
                    {
                        columnas.RelativeColumn(1.2f);
                        columnas.RelativeColumn(2.6f);
                        columnas.RelativeColumn(1);
                        columnas.RelativeColumn(1);
                        columnas.RelativeColumn(1.5f);
                        columnas.RelativeColumn(1.2f);
                        columnas.RelativeColumn(2);
                    });

                    tabla.Header(header =>
                    {
                        header.Cell().Text("Fecha").SemiBold();
                        header.Cell().Text("Acción").SemiBold();
                        header.Cell().Text("Tipo").SemiBold();
                        header.Cell().Text("Severidad").SemiBold();
                        header.Cell().Text("Usuario").SemiBold();
                        header.Cell().Text("IP").SemiBold();
                        header.Cell().Text("Dispositivo").SemiBold();
                    });

                    foreach (var evento in eventos)
                    {
                        tabla.Cell().PaddingTop(4).Text(evento.CreadoEn.ToString("dd/MM/yyyy HH:mm"));
                        tabla.Cell().PaddingTop(4).Text(EtiquetasAuditoria.Accion(evento.Accion));
                        tabla.Cell().PaddingTop(4).Text(EtiquetaTipo(evento.Tipo));
                        tabla.Cell().PaddingTop(4).Text(EtiquetaSeveridad(evento.Severidad));
                        tabla.Cell().PaddingTop(4).Text(evento.NombreUsuario ?? "—");
                        tabla.Cell().PaddingTop(4).Text(evento.Ip ?? "—");
                        tabla.Cell().PaddingTop(4).Text(evento.Dispositivo ?? "—");
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });

        return new ArchivoExportacion
        {
            Contenido = documento.GeneratePdf(),
            ContentType = "application/pdf",
            NombreArchivo = $"kubix_auditoria_{DateTimeOffset.UtcNow:yyyy-MM-dd}.pdf"
        };
    }

    private static string EtiquetaTipo(string tipo) =>
        tipo.ToLowerInvariant() switch
        {
            "sos" => "SOS",
            "auth" => "Autenticación",
            "admin" => "Administración",
            "system" => "Sistema",
            _ => tipo
        };

    private static string EtiquetaSeveridad(string severidad) =>
        severidad.ToLowerInvariant() switch
        {
            "high" => "Alta",
            "medium" => "Media",
            "low" => "Baja",
            _ => severidad
        };
}
