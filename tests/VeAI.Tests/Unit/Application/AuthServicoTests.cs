using Microsoft.Extensions.Time.Testing;
using VeAI.Application.Abstracoes;
using VeAI.Application.Dtos;
using VeAI.Application.Servicos;
using VeAI.Domain.Entities;
using VeAI.Domain.Excecoes;

namespace VeAI.Tests.Unit.Application;

/// <summary>Serviço de autenticação isolado do banco e da criptografia (dublês em memória).</summary>
public class AuthServicoTests
{
    private readonly UsuariosEmMemoria _usuarios = new();
    private readonly AuthServico _servico;

    public AuthServicoTests()
    {
        _servico = new AuthServico(_usuarios, _usuarios, new HasherFalso(), new TokenFalso(), new FakeTimeProvider());
    }

    [Fact(DisplayName = "registrar cria CIDADAO com e-mail normalizado e senha com hash")]
    public async Task Registrar()
    {
        var resposta = await _servico.RegistrarAsync(new RegistroRequest { Nome = " Ana ", Email = " Ana@VEAI.dev ", Senha = "12345678" });

        Assert.Equal("ana@veai.dev", resposta.Email);
        Assert.Equal("hash:12345678", _usuarios.Todos.Single().SenhaHash);
        Assert.Equal(1, _usuarios.Salvamentos);
    }

    [Fact(DisplayName = "registrar com e-mail já usado → conflito, sem salvar")]
    public async Task RegistrarRepetido()
    {
        await _servico.RegistrarAsync(new RegistroRequest { Nome = "Ana", Email = "ana@veai.dev", Senha = "12345678" });

        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() =>
            _servico.RegistrarAsync(new RegistroRequest { Nome = "Outra", Email = "ANA@veai.dev", Senha = "12345678" }));

        Assert.Equal(TipoErro.Conflito, erro.Tipo);
        Assert.Equal(1, _usuarios.Salvamentos);
    }

    [Fact(DisplayName = "login: e-mail inexistente e senha errada dão o mesmo erro (não revela se a conta existe)")]
    public async Task LoginInvalido()
    {
        await _servico.RegistrarAsync(new RegistroRequest { Nome = "Ana", Email = "ana@veai.dev", Senha = "12345678" });

        var semConta = await Assert.ThrowsAsync<RegraNegocioException>(() =>
            _servico.LoginAsync(new LoginRequest { Email = "x@veai.dev", Senha = "12345678" }));
        var senhaErrada = await Assert.ThrowsAsync<RegraNegocioException>(() =>
            _servico.LoginAsync(new LoginRequest { Email = "ana@veai.dev", Senha = "errada" }));

        Assert.Equal(TipoErro.NaoAutenticado, semConta.Tipo);
        Assert.Equal(semConta.Message, senhaErrada.Message);
    }

    [Fact(DisplayName = "login correto devolve token Bearer e os dados do usuário")]
    public async Task LoginOk()
    {
        await _servico.RegistrarAsync(new RegistroRequest { Nome = "Ana", Email = "ana@veai.dev", Senha = "12345678" });

        var token = await _servico.LoginAsync(new LoginRequest { Email = "ana@veai.dev", Senha = "12345678" });

        Assert.Equal("Bearer", token.Tipo);
        Assert.Equal("token-ana@veai.dev", token.Token);
        Assert.Equal("ana@veai.dev", token.Usuario.Email);
    }

    private sealed class UsuariosEmMemoria : IUsuarioRepositorio, IUnidadeDeTrabalho
    {
        public List<Usuario> Todos { get; } = [];
        public int Salvamentos { get; private set; }

        public Task<Usuario?> BuscarPorEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(Todos.SingleOrDefault(u => u.Email == email));
        public Task<Usuario?> BuscarComRegioesAsync(long id, CancellationToken ct = default) =>
            Task.FromResult(Todos.SingleOrDefault(u => u.Id == id));
        public Task<bool> EmailExisteAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(Todos.Any(u => u.Email == email));
        public void Adicionar(Usuario usuario) => Todos.Add(usuario);
        public Task SalvarAsync(CancellationToken ct = default)
        {
            Salvamentos++;
            return Task.CompletedTask;
        }
        public async Task<T> EmTransacaoAsync<T>(Func<Task<T>> acao, CancellationToken ct = default)
        {
            var r = await acao();
            await SalvarAsync(ct);
            return r;
        }
        public void AposConfirmar(Func<Task> acao) => acao();
    }

    private sealed class HasherFalso : ISenhaHasher
    {
        public string Gerar(string senha) => "hash:" + senha;
        public bool Confere(string senha, string hash) => hash == "hash:" + senha;
    }

    private sealed class TokenFalso : IGeradorToken
    {
        public TokenEmitido Emitir(Usuario usuario) => new("token-" + usuario.Email, DateTime.UtcNow.AddHours(8));
    }
}
