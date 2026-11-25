using Microsoft.EntityFrameworkCore;
using ConsultasPlan.Data.Converters;
using ConsultasPlan.Models;

namespace ConsultasPlan.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Afiliado> Afiliados { get; set; }
    public DbSet<Consulta> Consultas { get; set; }
    public DbSet<Profesional> Profesionales { get; set; }
    public DbSet<Sede> Sedes { get; set; }
    public DbSet<Especialidad> Especialidades { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcNullableDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Consulta>()
            .Property(c => c.Estado)
            .HasConversion<string>();

        modelBuilder.Entity<Consulta>()
            .Property(c => c.Modalidad)
            .HasConversion<string>();

        // Restrict, no SetNull: con baja lógica el borrado físico ya no es parte
        // del flujo, y si alguien lo intentara la base debe negarse en lugar de
        // dejar consultas sin afiliado. Es defensa en profundidad sobre la misma
        // decisión que toma el controlador.
        modelBuilder.Entity<Consulta>()
            .HasOne(c => c.Afiliado)
            .WithMany()
            .HasForeignKey(c => c.AfiliadoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Las propiedades se renombraron a la convención de .NET; las columnas
        // conservan su nombre para no requerir una migración de esquema.
        modelBuilder.Entity<Afiliado>().Property(a => a.FechaAlta).HasColumnName("createdAt");
        modelBuilder.Entity<Afiliado>().Property(a => a.Activo).HasColumnName("isActive");

        ConfigurarCatalogoDeEspecialidades(modelBuilder);
    }

    /// <summary>
    /// El catálogo de especialidades es parte del esquema, no datos de demo.
    /// </summary>
    /// <remarks>
    /// Va en la migración con <c>HasData</c> porque una regla de negocio depende
    /// de él: sin la fila <c>NUT</c> la política de cupo no se aplica a nada. Los
    /// afiliados y consultas de ejemplo, en cambio, son datos de demostración y
    /// viven en <see cref="DbSeeder"/>.
    ///
    /// El índice único sobre <c>Codigo</c> es lo que sostiene que el código sea
    /// identidad. Sin él es texto libre con otro nombre.
    /// </remarks>
    private static void ConfigurarCatalogoDeEspecialidades(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Especialidad>(e =>
        {
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.Codigo).HasMaxLength(16).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();

            e.HasData(
                new Especialidad { Id = 1, Codigo = "CLI", Nombre = "Clínica Médica" },
                new Especialidad { Id = 2, Codigo = "PED", Nombre = "Pediatría" },
                new Especialidad { Id = 3, Codigo = "CAR", Nombre = "Cardiología" },
                new Especialidad { Id = 4, Codigo = CodigosDeEspecialidad.Nutricion, Nombre = "Nutrición" },
                new Especialidad { Id = 5, Codigo = "TRA", Nombre = "Traumatología" });
        });

        // Restrict: borrar una especialidad del catálogo dejaría profesionales
        // sin especialidad y, con ellos, consultas cuya pertenencia al cupo deja
        // de poder determinarse.
        modelBuilder.Entity<Profesional>()
            .HasOne(p => p.Especialidad)
            .WithMany()
            .HasForeignKey(p => p.EspecialidadId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
