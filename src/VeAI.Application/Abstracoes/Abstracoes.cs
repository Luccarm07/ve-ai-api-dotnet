using VeAI.Domain.Entities;

namespace VeAI.Application.Abstracoes;

// Portas da camada de aplicação. A Infrastructure implementa (EF Core, BCrypt, JWT);
// os serviços dependem só destas interfaces, o que permite testá-los sem banco.

public interface IUsuarioRepositorio
{
    Task<Usuario?> BuscarPorEmailAsync(string emailNormalizado, CancellationToken ct = default);
    Task<Usuario?> BuscarComRegioesAsync(long id, CancellationToken ct = default);
    Task<bool> EmailExisteAsync(string emailNormalizado, CancellationToken ct = default);
    void Adicionar(Usuario usuario);
}

public interface IRegiaoRepositorio
{
    Task<IReadOnlyList<Regiao>> ListarAsync(CancellationToken ct = default);
}

/// <summary>Grava de uma vez as alterações feitas nos repositórios (SaveChanges).</summary>
public interface IUnidadeDeTrabalho
{
    Task SalvarAsync(CancellationToken ct = default);
}

public interface ISenhaHasher
{
    string Gerar(string senha);
    bool Confere(string senha, string hash);
}

public sealed record TokenEmitido(string Token, DateTime ExpiraEm);

public interface IGeradorToken
{
    TokenEmitido Emitir(Usuario usuario);
}
