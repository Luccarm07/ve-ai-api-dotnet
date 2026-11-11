using Microsoft.EntityFrameworkCore;
using VeAI.Application.Abstracoes;
using VeAI.Domain.Entities;

namespace VeAI.Infrastructure.Persistence.Repositorios;

internal sealed class UsuarioRepositorio(VeAiDbContext db) : IUsuarioRepositorio
{
    public Task<Usuario?> BuscarPorEmailAsync(string emailNormalizado, CancellationToken ct = default) =>
        db.Usuarios.Include(u => u.Regioes).SingleOrDefaultAsync(u => u.Email == emailNormalizado, ct);

    public Task<Usuario?> BuscarComRegioesAsync(long id, CancellationToken ct = default) =>
        db.Usuarios.Include(u => u.Regioes).SingleOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> EmailExisteAsync(string emailNormalizado, CancellationToken ct = default) =>
        db.Usuarios.AnyAsync(u => u.Email == emailNormalizado, ct);

    public void Adicionar(Usuario usuario) => db.Usuarios.Add(usuario);
}

internal sealed class RegiaoRepositorio(VeAiDbContext db) : IRegiaoRepositorio
{
    public async Task<IReadOnlyList<Regiao>> ListarAsync(CancellationToken ct = default) =>
        await db.Regioes.AsNoTracking().OrderBy(r => r.Nome).ToListAsync(ct);
}

internal sealed class UnidadeDeTrabalho(VeAiDbContext db) : IUnidadeDeTrabalho
{
    public Task SalvarAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
