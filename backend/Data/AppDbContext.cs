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
    }
}
