namespace Kubix.Application.Viajes;

public sealed class ExcepcionViajes : Exception
{
    public int CodigoEstado { get; }
    public string Titulo { get; }
    public string Codigo { get; }

    public ExcepcionViajes(int codigoEstado, string titulo, string detalle, string codigo)
        : base(detalle)
    {
        CodigoEstado = codigoEstado;
        Titulo = titulo;
        Codigo = codigo;
    }

    public static ExcepcionViajes NoEncontrado(string detalle, string codigo = "not_found") =>
        new(StatusCodes.Status404NotFound, "Not Found", detalle, codigo);

    public static ExcepcionViajes Prohibido(string detalle, string codigo = "forbidden") =>
        new(StatusCodes.Status403Forbidden, "Forbidden", detalle, codigo);

    public static ExcepcionViajes Validacion(string detalle, string codigo = "validation_error") =>
        new(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", detalle, codigo);

    public static ExcepcionViajes Conflicto(string detalle, string codigo = "conflict") =>
        new(StatusCodes.Status409Conflict, "Conflict", detalle, codigo);
}

file static class StatusCodes
{
    public const int Status403Forbidden = 403;
    public const int Status404NotFound = 404;
    public const int Status409Conflict = 409;
    public const int Status422UnprocessableEntity = 422;
}
