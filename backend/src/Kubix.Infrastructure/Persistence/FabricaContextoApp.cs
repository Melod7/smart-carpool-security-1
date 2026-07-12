using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Kubix.Infrastructure.Persistence;

public class FabricaContextoApp : IDesignTimeDbContextFactory<ContextoApp>
{
    public ContextoApp CreateDbContext(string[] args)
    {
        var rutaBase = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Kubix.Api"));
        if (!Directory.Exists(rutaBase))
        {
            rutaBase = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "Kubix.Api"));
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(rutaBase)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cadenaConexion = config.GetConnectionString("Default")
            ?? "Host=localhost;Port=55432;Database=kubix;Username=kubix;Password=kubix";

        var opciones = new DbContextOptionsBuilder<ContextoApp>()
            .UseNpgsql(cadenaConexion)
            .Options;

        return new ContextoApp(opciones);
    }
}
