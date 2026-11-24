using Microsoft.EntityFrameworkCore;
using ConsultasPlan.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=consultas.db"));

// El reloj se inyecta en lugar de leerse de DateTime.Now. Las reglas de negocio
// dependen del tiempo (ventana de cancelación, año en curso, cupo mensual) y
// sin esto sus bordes no se pueden verificar: habría que esperar a que el reloj
// del sistema llegue al instante exacto.
builder.Services.AddSingleton(TimeProvider.System);

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
    db.Database.EnsureCreated();
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
