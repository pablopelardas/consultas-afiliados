using ConsultasPlan.Data;
using ConsultasPlan.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ConsultasPlan.Tests.Fechas;

/// <summary>
/// El flujo de fechas contra la base real.
/// </summary>
/// <remarks>
/// Estos tests usan SQLite en memoria, no el proveedor InMemory de EF. El
/// problema que verifican es específico de SQLite —que no tiene tipo fecha y
/// guarda texto sin zona—, y el proveedor InMemory conserva los objetos .NET
/// tal cual: no reproduce la pérdida de <see cref="DateTimeKind"/> y daría
/// verde sin probar nada.
/// </remarks>
public class PersistenciaUtcTests : IDisposable
{
    private readonly SqliteConnection _conexion;

    public PersistenciaUtcTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
    }

    public void Dispose() => _conexion.Dispose();

    private AppDbContext CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conexion)
            .Options;

        var db = new AppDbContext(opciones);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public void Una_fecha_guardada_en_UTC_se_lee_con_Kind_Utc()
    {
        var instante = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        using (var db = CrearContexto())
        {
            db.Afiliados.Add(new Afiliado
            {
                NombreCompleto = "Afiliado",
                DNI = "30000000",
                FechaAlta = instante,
                Activo = true
            });
            db.SaveChanges();
        }

        using (var db = CrearContexto())
        {
            var leido = db.Afiliados.Single();

            Assert.Equal(DateTimeKind.Utc, leido.FechaAlta.Kind);
            Assert.Equal(instante, leido.FechaAlta);
        }
    }

    // Sin el converter, SQLite devolvía Kind.Unspecified y toda conversión
    // posterior lo trataba como hora local: el instante se corría tantas horas
    // como el huso del servidor, sin error visible.
    [Fact]
    public void Una_fecha_leida_se_puede_convertir_a_local_sin_desviarse()
    {
        var instante = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        using (var db = CrearContexto())
        {
            db.Afiliados.Add(new Afiliado
            {
                NombreCompleto = "Afiliado",
                DNI = "30000000",
                FechaAlta = instante,
                Activo = true
            });
            db.SaveChanges();
        }

        using (var db = CrearContexto())
        {
            var leido = db.Afiliados.Single();

            Assert.Equal(instante, leido.FechaAlta.ToUniversalTime());
        }
    }

    [Fact]
    public void Una_fecha_nullable_tambien_conserva_el_Kind()
    {
        var bloqueo = new DateTime(2026, 4, 19, 9, 30, 0, DateTimeKind.Utc);

        using (var db = CrearContexto())
        {
            db.Afiliados.Add(new Afiliado
            {
                NombreCompleto = "Afiliado bloqueado",
                DNI = "30000001",
                Bloqueado = true,
                FechaBloqueo = bloqueo,
                Activo = true
            });
            db.SaveChanges();
        }

        using (var db = CrearContexto())
        {
            var leido = db.Afiliados.Single();

            Assert.NotNull(leido.FechaBloqueo);
            Assert.Equal(DateTimeKind.Utc, leido.FechaBloqueo!.Value.Kind);
            Assert.Equal(bloqueo, leido.FechaBloqueo.Value);
        }
    }

    [Fact]
    public void Una_fecha_nullable_sin_valor_sigue_siendo_nula()
    {
        using (var db = CrearContexto())
        {
            db.Afiliados.Add(new Afiliado
            {
                NombreCompleto = "Afiliado activo",
                DNI = "30000002",
                FechaBloqueo = null,
                Activo = true
            });
            db.SaveChanges();
        }

        using (var db = CrearContexto())
        {
            Assert.Null(db.Afiliados.Single().FechaBloqueo);
        }
    }

    // Una fecha en hora local se normaliza a UTC al persistirse, de modo que el
    // instante almacenado es el mismo sin importar desde qué huso se escribió.
    [Fact]
    public void Una_fecha_en_hora_local_se_normaliza_a_UTC_al_guardarse()
    {
        var local = new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Local);

        using (var db = CrearContexto())
        {
            db.Afiliados.Add(new Afiliado
            {
                NombreCompleto = "Afiliado",
                DNI = "30000003",
                FechaAlta = local,
                Activo = true
            });
            db.SaveChanges();
        }

        using (var db = CrearContexto())
        {
            var leido = db.Afiliados.Single();

            Assert.Equal(DateTimeKind.Utc, leido.FechaAlta.Kind);
            Assert.Equal(local.ToUniversalTime(), leido.FechaAlta);
        }
    }
}
