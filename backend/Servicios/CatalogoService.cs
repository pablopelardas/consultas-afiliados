using ConsultasPlan.Data;
using ConsultasPlan.Models;
using Microsoft.EntityFrameworkCore;

namespace ConsultasPlan.Servicios;

public class CatalogoService : ICatalogoService
{
    private readonly AppDbContext _contexto;

    public CatalogoService(AppDbContext contexto) => _contexto = contexto;

    public async Task<IReadOnlyList<Profesional>> ListarProfesionales() =>
        await _contexto.Profesionales.Include(p => p.Sede).ToListAsync();

    public async Task<IReadOnlyList<Sede>> ListarSedes() =>
        await _contexto.Sedes.ToListAsync();
}
