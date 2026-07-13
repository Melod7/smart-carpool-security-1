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
        new(StatusCodes.Status404NotFound, "Not Found", detalle, codigo);
}

file static class StatusCodes
{
    public const int Status404NotFound = 404;
}
