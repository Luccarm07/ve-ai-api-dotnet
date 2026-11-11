using VeAI.Application.Abstracoes;
using VeAI.Application.Dtos;
using VeAI.Domain.Entities;
using VeAI.Domain.Excecoes;

namespace VeAI.Application.Servicos;

public sealed class AuthServico(IUsuarioRepositorio usuarios, IUnidadeDeTrabalho uow, ISenhaHasher hasher,
                                IGeradorToken tokens, TimeProvider relogio)
{
    /// <summary>Hash válido usado quando o e-mail não existe: o tempo de resposta não revela se a conta existe.</summary>
    private const string HashFicticio = "$2a$10$xCHtq1/BEvjc2Bwk9iQ37O84OOuuVQPqIF/pYmbI0hxqFP3jXuOBi";

    /// <summary>O cadastro aberto cria apenas CIDADAO. Gestores vêm do seed.</summary>
    public async Task<UsuarioResposta> RegistrarAsync(RegistroRequest req, CancellationToken ct = default)
    {
        var email = Usuario.NormalizarEmail(req.Email!);
        if (await usuarios.EmailExisteAsync(email, ct))
            throw RegraNegocioException.Conflito("Já existe uma conta com este e-mail.");

        var novo = Usuario.NovoCidadao(req.Nome!, email, hasher.Gerar(req.Senha!), relogio.GetUtcNow().UtcDateTime);
        usuarios.Adicionar(novo);
        await uow.SalvarAsync(ct);
        return UsuarioResposta.De(novo);
    }

    public async Task<TokenResposta> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var usuario = await usuarios.BuscarPorEmailAsync(Usuario.NormalizarEmail(req.Email!), ct);
        var senhaConfere = hasher.Confere(req.Senha!, usuario?.SenhaHash ?? HashFicticio);
        if (usuario is null || !senhaConfere)
            throw new RegraNegocioException(TipoErro.NaoAutenticado, "E-mail ou senha incorretos.");

        var token = tokens.Emitir(usuario);
        return new TokenResposta(token.Token, "Bearer", token.ExpiraEm, usuario.Papel, UsuarioResposta.De(usuario));
    }
}

public sealed class UsuarioServico(IUsuarioRepositorio usuarios, IUnidadeDeTrabalho uow)
{
    public async Task<UsuarioResposta> BuscarAsync(long id, CancellationToken ct = default) =>
        UsuarioResposta.De(await CarregarAsync(id, ct));

    public async Task<UsuarioResposta> AtualizarPreferenciasAsync(long id, PreferenciasRequest req, CancellationToken ct = default)
    {
        var usuario = await CarregarAsync(id, ct);
        usuario.AtualizarPreferencias(req.VelocidadeVoz!.Value, req.PerguntaLivreAtiva!.Value);
        await uow.SalvarAsync(ct);
        return UsuarioResposta.De(usuario);
    }

    private async Task<Usuario> CarregarAsync(long id, CancellationToken ct) =>
        await usuarios.BuscarComRegioesAsync(id, ct) ?? throw RegraNegocioException.NaoEncontrado("Usuário não encontrado.");
}

public sealed class RegiaoServico(IRegiaoRepositorio regioes)
{
    public async Task<IReadOnlyList<RegiaoResposta>> ListarAsync(CancellationToken ct = default) =>
        (await regioes.ListarAsync(ct)).Select(RegiaoResposta.De).ToList();
}
