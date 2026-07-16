using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Kubix.Tests;

/// <summary>KBX-27: integración contra Postgres real vía Testcontainers.</summary>
public sealed class FixturePostgres : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString =>
        _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container no iniciado.");

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("kubix_test")
            .WithUsername("kubix")
            .WithPassword("kubix")
            .Build();
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public ContextoApp CrearDb()
    {
        var opciones = new DbContextOptionsBuilder<ContextoApp>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new ContextoApp(opciones, new ContextoInquilino { OmitirFiltros = true });
    }
}

[CollectionDefinition("postgres")]
public sealed class ColeccionPostgres : ICollectionFixture<FixturePostgres>;

[Collection("postgres")]
public class PruebasPostgresTestcontainers
{
    private readonly FixturePostgres _fixture;

    public PruebasPostgresTestcontainers(FixturePostgres fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_crea_tablas_y_permite_insertar_universidad()
    {
        await using var db = _fixture.CrearDb();
        await db.Database.MigrateAsync();

        var uni = new Domain.Entities.Universidad
        {
            Nombre = "Test U",
            Slug = $"test-{Guid.NewGuid():N}"[..12],
            Estado = EstadoUniversidad.Activa
        };
        db.Universidades.Add(uni);
        await db.SaveChangesAsync();

        Assert.True(await db.Universidades.AnyAsync(u => u.Id == uni.Id));
    }
}
