using ConsultasPlan.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ConsultasPlan.Tests.Migraciones;

/// <summary>
/// La migración del catálogo de especialidades convierte los datos existentes
/// en lugar de perderlos, y lo hace en dos etapas.
/// </summary>
/// <remarks>
/// Estos tests levantan una base con el esquema del código base, le ponen datos
/// como los que tendría un ambiente real —incluido un profesional con una
/// especialidad que el catálogo no contempla— y verifican qué pasa al migrar.
///
/// La separación en dos etapas no es ceremonia: la etapa 1 es siempre segura y
/// deja los datos a medio convertir visibles, y la etapa 2 se niega a completar
/// si alguien quedó sin clasificar.
/// </remarks>
public class NormalizacionDeEspecialidadesTests : IDisposable
{
    private const string EsquemaDelCodigoBase = "Inicial";
    private const string Etapa1 = "CatalogoDeEspecialidades";

    private readonly SqliteConnection _conexion;

    public NormalizacionDeEspecialidadesTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
    }

    public void Dispose() => _conexion.Dispose();

    private AppDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexion).Options);

    private static void MigrarHasta(AppDbContext db, string migracion) =>
        db.GetService<IMigrator>().Migrate(migracion);

    /// <summary>
    /// Base con el esquema anterior al catálogo y profesionales cargados con la
    /// especialidad como texto, igual que los sembraba el código base.
    /// </summary>
    /// <param name="conUnaEspecialidadDesconocida">
    /// Agrega un profesional cuyo texto no figura en el catálogo: el caso que
    /// ninguna migración puede resolver sola.
    /// </param>
    private void SembrarComoElCodigoBase(bool conUnaEspecialidadDesconocida)
    {
        using var db = CrearContexto();
        MigrarHasta(db, EsquemaDelCodigoBase);

        db.Database.ExecuteSqlRaw("""
            INSERT INTO Sedes (Nombre, Direccion) VALUES ('Belgrano', 'Av. Cabildo 1200');
            INSERT INTO Profesionales (NombreCompleto, Especialidad, SedeId) VALUES
                ('Lic. Julieta Arana', 'Nutrición',      1),
                ('Dr. Alejandro Rios', 'Clínica Médica', 1);
            """);

        if (conUnaEspecialidadDesconocida)
            db.Database.ExecuteSqlRaw("""
                INSERT INTO Profesionales (NombreCompleto, Especialidad, SedeId)
                VALUES ('Dra. Paula Sin Match', 'Homeopatía', 1);
                """);
    }

    public class Etapa1Conversion : NormalizacionDeEspecialidadesTests
    {
        [Fact]
        public void El_texto_existente_se_convierte_en_referencia_al_catalogo()
        {
            SembrarComoElCodigoBase(conUnaEspecialidadDesconocida: true);

            using var db = CrearContexto();
            MigrarHasta(db, Etapa1);

            var convertidos = db.Database
                .SqlQuery<string>($"""
                    SELECT p.NombreCompleto || ' -> ' || COALESCE(e.Codigo, 'SIN CLASIFICAR') AS Value
                      FROM Profesionales p
                      LEFT JOIN Especialidades e ON e.Id = p.EspecialidadId
                     ORDER BY p.Id
                    """)
                .ToList();

            Assert.Equal([
                "Lic. Julieta Arana -> NUT",
                "Dr. Alejandro Rios -> CLI",
                "Dra. Paula Sin Match -> SIN CLASIFICAR"
            ], convertidos);
        }

        // La etapa 1 no destruye nada: la columna de texto sigue ahí, que es lo
        // que permite reintentar la conversión o revisarla a mano.
        [Fact]
        public void El_texto_original_se_conserva()
        {
            SembrarComoElCodigoBase(conUnaEspecialidadDesconocida: true);

            using var db = CrearContexto();
            MigrarHasta(db, Etapa1);

            var textos = db.Database
                .SqlQuery<string>($"SELECT Especialidad AS Value FROM Profesionales ORDER BY Id")
                .ToList();

            Assert.Equal(["Nutrición", "Clínica Médica", "Homeopatía"], textos);
        }

        [Fact]
        public void El_catalogo_queda_creado_con_sus_filas()
        {
            SembrarComoElCodigoBase(conUnaEspecialidadDesconocida: false);

            using var db = CrearContexto();
            MigrarHasta(db, Etapa1);

            Assert.Equal(5, db.Especialidades.Count());
            Assert.Single(db.Especialidades.Where(e => e.Codigo == "NUT"));
        }

        // La consulta de control que se corre entre las dos etapas.
        [Fact]
        public void Lo_que_no_se_pudo_convertir_queda_visible()
        {
            SembrarComoElCodigoBase(conUnaEspecialidadDesconocida: true);

            using var db = CrearContexto();
            MigrarHasta(db, Etapa1);

            var sinClasificar = db.Database
                .SqlQuery<string>($"""
                    SELECT NombreCompleto || ': ' || Especialidad AS Value
                      FROM Profesionales
                     WHERE EspecialidadId IS NULL
                    """)
                .ToList();

            Assert.Equal(["Dra. Paula Sin Match: Homeopatía"], sinClasificar);
        }
    }

    public class Etapa2Cierre : NormalizacionDeEspecialidadesTests
    {
        [Fact]
        public void Con_todo_convertido_la_migracion_se_completa()
        {
            SembrarComoElCodigoBase(conUnaEspecialidadDesconocida: false);

            using var db = CrearContexto();
            db.Database.Migrate();

            var nutricionista = db.Profesionales
                .Include(p => p.Especialidad)
                .Single(p => p.NombreCompleto == "Lic. Julieta Arana");

            Assert.Equal("NUT", nutricionista.Especialidad!.Codigo);
            Assert.Equal("Nutrición", nutricionista.Especialidad.Nombre);
        }

        // El punto de separar las etapas: la segunda se niega a completar con
        // datos a medio convertir, en lugar de inventar una especialidad.
        [Fact]
        public void Con_alguien_sin_clasificar_la_migracion_falla_en_lugar_de_adivinar()
        {
            SembrarComoElCodigoBase(conUnaEspecialidadDesconocida: true);

            using var db = CrearContexto();

            Assert.ThrowsAny<Exception>(() => db.Database.Migrate());
        }

        // Y al fallar, la etapa 1 sigue aplicada: los datos quedan donde se los
        // puede revisar, no en un estado intermedio irrecuperable.
        [Fact]
        public void Y_al_fallar_los_datos_siguen_siendo_revisables()
        {
            SembrarComoElCodigoBase(conUnaEspecialidadDesconocida: true);

            using (var intento = CrearContexto())
                Assert.ThrowsAny<Exception>(() => intento.Database.Migrate());

            using var db = CrearContexto();
            var textos = db.Database
                .SqlQuery<string>($"SELECT Especialidad AS Value FROM Profesionales ORDER BY Id")
                .ToList();

            Assert.Contains("Homeopatía", textos);
        }

        // Una migración cuyo Down deja la columna vacía no es reversible, es
        // destructiva.
        [Fact]
        public void La_vuelta_atras_repone_el_texto_desde_el_catalogo()
        {
            SembrarComoElCodigoBase(conUnaEspecialidadDesconocida: false);

            using (var db = CrearContexto()) db.Database.Migrate();
            using (var db = CrearContexto()) MigrarHasta(db, EsquemaDelCodigoBase);

            using var verificacion = CrearContexto();
            var textos = verificacion.Database
                .SqlQuery<string>($"SELECT Especialidad AS Value FROM Profesionales ORDER BY Id")
                .ToList();

            Assert.Equal(["Nutrición", "Clínica Médica"], textos);
        }
    }

    // Una base nueva no pasa por la conversión, pero sí tiene que terminar con
    // el catálogo: la fila NUT es lo que hace que la política se aplique.
    [Fact]
    public void Una_base_nueva_queda_con_el_catalogo_completo()
    {
        using var db = CrearContexto();
        db.Database.Migrate();

        Assert.Single(db.Especialidades.Where(e => e.Codigo == "NUT"));
        Assert.Empty(db.Profesionales);
    }
}
