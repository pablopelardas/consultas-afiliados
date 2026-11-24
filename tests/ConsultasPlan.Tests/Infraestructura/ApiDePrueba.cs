using ConsultasPlan.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ConsultasPlan.Tests.Infraestructura;

/// <summary>
/// Levanta la API completa en memoria para verificar lo que solo existe en el
/// pipeline HTTP: ruteo, verbos, códigos de respuesta y formato de error.
/// </summary>
/// <remarks>
/// Instanciar el controlador directamente es más rápido y alcanza para la
/// lógica, pero no puede responder si una ruta acepta GET: eso lo decide el
/// ruteo, no el método. Para afirmar que cancelar dejó de responder a GET hay
/// que atravesar el pipeline.
///
/// SQLite en memoria reemplaza al archivo: cada instancia arranca con su propia
/// base vacía y los tests no se pisan entre sí.
/// </remarks>
public class ApiDePrueba : WebApplicationFactory<Program>
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conexion;

    public FakeTimeProvider Reloj { get; } = Infraestructura.Reloj.Fijo();

    public ApiDePrueba()
    {
        _conexion = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        _conexion.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // ConfigureTestServices corre DESPUÉS de los registros de la aplicación:
        // es el punto donde una sustitución efectivamente reemplaza al original.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_conexion));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Reloj);
        });
    }

    public AppDbContext CrearContexto()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _conexion.Dispose();
    }
}
