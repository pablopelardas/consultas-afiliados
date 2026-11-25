using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ConsultasPlan.Data;

/// <summary>
/// Contexto para las herramientas de línea de comandos de EF Core.
/// </summary>
/// <remarks>
/// Sin esta fábrica, <c>dotnet ef</c> arranca la aplicación para obtener el
/// contexto, lo que ejecuta la migración y el sembrado antes de poder generar
/// una migración nueva. Acá el contexto se construye solo: generar o inspeccionar
/// migraciones no toca ninguna base.
/// </remarks>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=migraciones.db")
            .Options;

        return new AppDbContext(opciones);
    }
}
