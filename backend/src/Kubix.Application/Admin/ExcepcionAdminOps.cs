namespace Kubix.Application.Admin;

public sealed class ExcepcionAdminOps : Exception
{
    public int CodigoEstado { get; }
    public string Titulo { get; }
    public string Codigo { get; }

    public ExcepcionAdminOps(int codigoEstado, string titulo, string detalle, string codigo)
        : base(detalle)
    {
        CodigoEstado = codigoEstado;
        Titulo = titulo;
        Codigo = codigo;
    }

    public static ExcepcionAdminOps NoEncontrado(string detalle, string codigo = "not_found") =>
        new(StatusCodes.Status404NotFound, "Not Found", detalle, codigo);

    public static ExcepcionAdminOps Validacion(string detalle, string codigo = "validation_error") =>
        new(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", detalle, codigo);

    public static ExcepcionAdminOps Prohibido(string detalle, string codigo = "forbidden") =>
        new(StatusCodes.Status403Forbidden, "Forbidden", detalle, codigo);

    public static ExcepcionAdminOps ErrorInterno(string detalle, string codigo = "export_failed") =>
        new(StatusCodes.Status500InternalServerError, "Internal Server Error", detalle, codigo);
}

file static class StatusCodes
{
    public const int Status403Forbidden = 403;
    public const int Status404NotFound = 404;
    public const int Status422UnprocessableEntity = 422;
    public const int Status500InternalServerError = 500;
}
