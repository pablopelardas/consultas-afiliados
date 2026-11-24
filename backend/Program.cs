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
    options.JsonSerializerOptions.ReferenceHandler =
        System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.MapControllers();
app.Run();
