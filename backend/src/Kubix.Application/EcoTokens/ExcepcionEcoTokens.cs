namespace Kubix.Application.EcoTokens;

public sealed class ExcepcionEcoTokens : Exception
{
    public int CodigoEstado { get; }
    public string Titulo { get; }
    public string Codigo { get; }

    public ExcepcionEcoTokens(int codigoEstado, string titulo, string detalle, string codigo)
        : base(detalle)
    {
        CodigoEstado = codigoEstado;
        Titulo = titulo;
        Codigo = codigo;
    }

    public static ExcepcionEcoTokens NoEncontrado(string detalle, string codigo = "not_found") =>
        new(404, "Not Found", detalle, codigo);

    public static ExcepcionEcoTokens Validacion(string detalle, string codigo = "validation_error") =>
        new(422, "Unprocessable Entity", detalle, codigo);

    public static ExcepcionEcoTokens Conflicto(string detalle, string codigo = "conflict") =>
        new(409, "Conflict", detalle, codigo);

    public static ExcepcionEcoTokens Prohibido(string detalle, string codigo = "forbidden") =>
        new(403, "Forbidden", detalle, codigo);
}
