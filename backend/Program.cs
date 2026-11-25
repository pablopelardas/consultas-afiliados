using Microsoft.EntityFrameworkCore;
using ConsultasPlan.Data;
using ConsultasPlan.Dominio.Cupo;
using ConsultasPlan.Servicios;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=consultas.db"));

// El reloj se inyecta en lugar de leerse de DateTime.Now. Las reglas de negocio
// dependen del tiempo (ventana de cancelación, año en curso, cupo mensual) y
// sin esto sus bordes no se pueden verificar: habría que esperar a que el reloj
// del sistema llegue al instante exacto.
builder.Services.AddSingleton(TimeProvider.System);

// Los controladores dependen de la interfaz, no de la implementación. Eso es lo
// que permite sustituir un servicio en un test sin tocar el controlador, y lo
// que mantiene a los controladores sin acceso a AppDbContext.
// Scoped porque comparten el ciclo de vida del DbContext del que dependen.
builder.Services.AddScoped<IConsultaService, ConsultaService>();
builder.Services.AddScoped<IAfiliadoService, AfiliadoService>();
builder.Services.AddScoped<ICatalogoService, CatalogoService>();
builder.Services.AddScoped<ICupoNutricionService, CupoNutricionService>();

// Parte 2 — política de cupo de nutrición.
builder.Services.Configure<CupoNutricionOptions>(
    builder.Configuration.GetSection(CupoNutricionOptions.Seccion));

// La política es pura y sin estado: Singleton alcanza.
builder.Services.AddSingleton<ICupoNutricionPolicy, CupoNutricionPolicy>();

// Acá se elige la interpretación de "año" y "mes" del enunciado. PeriodoVigencia
// es la lectura alternativa, implementada y testeada: cambiar de criterio es
// cambiar esta línea, sin tocar la política ni sus tests.
builder.Services.AddSingleton<IPeriodoCupoProvider, PeriodoCalendario>();

builder.Services.AddControllers().AddJsonOptions(options =>
{
    // ReferenceHandler.IgnoreCycles ya no hace falta: era un paliativo para
    // poder serializar entidades de EF con navegación circular. La API devuelve
    // contratos propios, que no tienen ciclos porque no los necesitan.
    options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Los errores se devuelven como ProblemDetails (RFC 7807): un formato único y
// documentado en lugar de un objeto anónimo distinto por endpoint. El cliente
// lee siempre el mismo campo para mostrarle algo útil al usuario.
builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var reloj = scope.ServiceProvider.GetRequiredService<TimeProvider>();

    // Migrate() y no EnsureCreated(): el segundo crea el esquema a partir del
    // modelo actual y no deja registro de versión, así que un cambio posterior
    // no tiene forma de aplicarse sobre una base existente. En un sistema
    // descrito como productivo, eso significa que el esquema no puede
    // evolucionar sin perder los datos.
    db.Database.Migrate();
    DbSeeder.Seed(db, reloj);
}

// Una excepción no controlada debe producir un 500 con ProblemDetails, no una
// página de error ni el stack trace. Va antes que todo lo demás para envolver
// al resto del pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.MapControllers();
app.Run();

// Expone la clase Program generada por los top-level statements para que
// WebApplicationFactory pueda levantar la API en los tests de integración.
public partial class Program { }
