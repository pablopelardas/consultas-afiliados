using ConsultasPlan.Contratos;
using ConsultasPlan.Data;
using ConsultasPlan.Models;
using Microsoft.EntityFrameworkCore;

namespace ConsultasPlan.Servicios;

public class AfiliadoService : IAfiliadoService
{
    private const string AfiliadoInexistente = "El afiliado indicado no existe.";

    private readonly AppDbContext _contexto;
    private readonly TimeProvider _reloj;

    public AfiliadoService(AppDbContext contexto, TimeProvider reloj)
    {
        _contexto = contexto;
        _reloj = reloj;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<Afiliado>> ListarVigentes() =>
        await _contexto.Afiliados.Where(a => a.Activo).ToListAsync();

    public async Task<ServiceResult<Afiliado>> Obtener(int id)
    {
        var afiliado = await Vigente(id);
        if (afiliado is null) return Falla.NoEncontrado(AfiliadoInexistente);
        return ServiceResult.Ok(afiliado);
    }

    public async Task<ServiceResult<Afiliado>> Crear(CrearAfiliadoRequest request)
    {
        // El DNI identifica a la persona: dos afiliados con el mismo documento
        // son el mismo afiliado cargado dos veces, y a partir de ahí el
        // historial y el cupo quedan partidos entre dos registros.
        if (await _contexto.Afiliados.AnyAsync(a => a.DNI == request.DNI))
            return Falla.Conflicto($"Ya existe un afiliado con el DNI {request.DNI}.");

        var afiliado = new Afiliado
        {
            NombreCompleto = request.NombreCompleto,
            DNI = request.DNI,
            Email = request.Email,
            Telefono = request.Telefono,
            Plan = request.Plan,
            FechaAlta = Ahora,
            Activo = true,
            FechaBaja = null
        };

        _contexto.Afiliados.Add(afiliado);
        await _contexto.SaveChangesAsync();
        return ServiceResult.Ok(afiliado);
    }

    public async Task<ServiceResult<Afiliado>> Actualizar(int id, ActualizarAfiliadoRequest request)
    {
        var afiliado = await Vigente(id);
        if (afiliado is null) return Falla.NoEncontrado(AfiliadoInexistente);

        if (await _contexto.Afiliados.AnyAsync(a => a.DNI == request.DNI && a.Id != id))
            return Falla.Conflicto($"Ya existe otro afiliado con el DNI {request.DNI}.");

        afiliado.NombreCompleto = request.NombreCompleto;
        afiliado.DNI = request.DNI;
        afiliado.Email = request.Email;
        afiliado.Telefono = request.Telefono;

        await _contexto.SaveChangesAsync();
        return ServiceResult.Ok(afiliado);
    }

    /// <remarks>
    /// El código base ejecutaba <c>Remove</c>. Con la clave foránea en
    /// <c>SetNull</c>, eso dejaba las consultas del afiliado sin paciente: se
    /// perdía de forma irreversible a quién correspondía cada atención médica.
    /// </remarks>
    public async Task<ServiceResult> DarDeBaja(int id)
    {
        var afiliado = await _contexto.Afiliados.FindAsync(id);
        if (afiliado is null) return Falla.NoEncontrado(AfiliadoInexistente);

        if (!afiliado.Activo)
            return Falla.Conflicto("El afiliado ya se encuentra dado de baja.");

        afiliado.Activo = false;
        afiliado.FechaBaja = Ahora;
        await _contexto.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <remarks>
    /// Una baja que no se puede deshacer es, en la práctica, un borrado con otro
    /// nombre. La reactivación es lo que distingue la baja lógica de un borrado
    /// que además ocupa lugar.
    /// </remarks>
    public async Task<ServiceResult<Afiliado>> Reactivar(int id)
    {
        var afiliado = await _contexto.Afiliados.FindAsync(id);
        if (afiliado is null) return Falla.NoEncontrado(AfiliadoInexistente);

        if (afiliado.Activo)
            return Falla.Conflicto("El afiliado ya se encuentra activo.");

        afiliado.Activo = true;
        afiliado.FechaBaja = null;
        await _contexto.SaveChangesAsync();
        return ServiceResult.Ok(afiliado);
    }

    private Task<Afiliado?> Vigente(int id) =>
        _contexto.Afiliados.FirstOrDefaultAsync(a => a.Id == id && a.Activo);
}
