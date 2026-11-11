using Microsoft.Extensions.Time.Testing;
using VeAI.Application.Abstracoes;
using VeAI.Application.Servicos;
using VeAI.Domain.Entities;
using VeAI.Domain.Excecoes;

namespace VeAI.Tests.Unit.Application;

/// <summary>Regras da idempotência isoladas do banco: quando executa, quando repete e quando recusa.</summary>
public class IdempotenciaServicoTests
{
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly RegistrosEmMemoria _registros = new();
    private readonly IdempotenciaServico _servico;
    private int _execucoes;

    public IdempotenciaServicoTests() => _servico = new IdempotenciaServico(_registros, _registros, _relogio);

    private Task<(int, string)> Acao()
    {
        _execucoes++;
        return Task.FromResult((201, $"relato-{_execucoes}"));
    }

    [Fact(DisplayName = "sem Idempotency-Key a ação sempre executa e nada é gravado")]
    public async Task SemChave()
    {
        await _servico.ExecutarAsync(1, null, new { a = 1 }, Acao);
        await _servico.ExecutarAsync(1, "  ", new { a = 1 }, Acao);

        Assert.Equal(2, _execucoes);
        Assert.Empty(_registros.Todos);
    }

    [Fact(DisplayName = "mesma chave e mesmo corpo: executa uma vez e repete a resposta gravada")]
    public async Task Repete()
    {
        var r1 = await _servico.ExecutarAsync(1, "k1", new { a = 1 }, Acao);
        var r2 = await _servico.ExecutarAsync(1, " k1 ", new { a = 1 }, Acao);

        Assert.Equal(1, _execucoes);
        Assert.False(r1.Repetida);
        Assert.True(r2.Repetida);
        Assert.Equal((201, "relato-1"), (r2.Status, r2.Corpo));
    }

    [Fact(DisplayName = "mesma chave com outro corpo → 422, sem executar")]
    public async Task OutroCorpo()
    {
        await _servico.ExecutarAsync(1, "k1", new { a = 1 }, Acao);

        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => _servico.ExecutarAsync(1, "k1", new { a = 2 }, Acao));

        Assert.Equal(TipoErro.RegraViolada, erro.Tipo);
        Assert.Equal(1, _execucoes);
    }

    [Fact(DisplayName = "depois de 24 h a chave vence e pode ser usada de novo")]
    public async Task Vencida()
    {
        await _servico.ExecutarAsync(1, "k1", new { a = 1 }, Acao);
        _relogio.Advance(TimeSpan.FromHours(25));

        var r = await _servico.ExecutarAsync(1, "k1", new { a = 1 }, Acao);

        Assert.False(r.Repetida);
        Assert.Equal(2, _execucoes);
    }

    [Fact(DisplayName = "se a ação falha, nada é gravado e a mesma chave pode ser tentada de novo")]
    public async Task FalhaNaoGrava()
    {
        await Assert.ThrowsAsync<RegraNegocioException>(() => _servico.ExecutarAsync<string>(1, "k1", new { a = 1 },
            () => throw RegraNegocioException.Regra("falhou")));

        var r = await _servico.ExecutarAsync(1, "k1", new { a = 1 }, Acao);

        Assert.False(r.Repetida);
        Assert.Equal(1, _execucoes);
    }

    [Fact(DisplayName = "o hash identifica o conteúdo: mesmo objeto, mesmo hash")]
    public void Hash()
    {
        Assert.Equal(IdempotenciaServico.Hash(new { a = 1, b = "x" }), IdempotenciaServico.Hash(new { a = 1, b = "x" }));
        Assert.NotEqual(IdempotenciaServico.Hash(new { a = 1 }), IdempotenciaServico.Hash(new { a = 2 }));
        Assert.Equal(64, IdempotenciaServico.Hash(new { a = 1 }).Length);
    }

    private sealed class RegistrosEmMemoria : IIdempotenciaRepositorio, IUnidadeDeTrabalho
    {
        private readonly List<RegistroIdempotencia> _pendentes = [];
        public List<RegistroIdempotencia> Todos { get; } = [];

        public Task<RegistroIdempotencia?> BuscarAsync(long usuarioId, string chave, CancellationToken ct = default) =>
            Task.FromResult(Todos.SingleOrDefault(r => r.UsuarioId == usuarioId && r.Chave == chave));
        public void Adicionar(RegistroIdempotencia registro) => _pendentes.Add(registro);
        public void Remover(RegistroIdempotencia registro) => Todos.Remove(registro);
        public Task<int> ApagarAnterioresAAsync(DateTime limite, CancellationToken ct = default) =>
            Task.FromResult(Todos.RemoveAll(r => r.CriadoEm < limite));

        public Task SalvarAsync(CancellationToken ct = default)
        {
            Todos.AddRange(_pendentes);
            _pendentes.Clear();
            return Task.CompletedTask;
        }

        public async Task<T> EmTransacaoAsync<T>(Func<Task<T>> acao, CancellationToken ct = default)
        {
            try
            {
                var r = await acao();
                await SalvarAsync(ct);
                return r;
            }
            catch
            {
                _pendentes.Clear(); // rollback
                throw;
            }
        }

        public void AposConfirmar(Func<Task> acao) => acao();
    }
}
