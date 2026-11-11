using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VeAI.Application.Abstracoes;
using VeAI.Domain.Entities;
using VeAI.Domain.Excecoes;

namespace VeAI.Application.Servicos;

/// <summary>Resposta de uma operação idempotente. Repetida = veio do registro gravado (nada foi executado de novo).</summary>
public sealed record RespostaIdempotente<T>(int Status, T Corpo, bool Repetida);

/// <summary>
/// Idempotência pelo header Idempotency-Key (tabela IDEMPOTENCIA):
/// - mesma chave e mesmo corpo em até 24 h → devolve a resposta gravada, sem executar de novo;
/// - mesma chave com outro corpo → 422;
/// - a ação e o registro da resposta são gravados na MESMA transação. Se duas requisições com a mesma chave
///   chegarem juntas, a segunda viola a chave única (usuário + chave), é desfeita e devolve a resposta da primeira.
/// </summary>
public sealed class IdempotenciaServico(IIdempotenciaRepositorio repositorio, IUnidadeDeTrabalho uow, TimeProvider relogio)
{
    public const string Header = "Idempotency-Key";
    public const string HeaderRepetida = "Idempotent-Replayed";
    public const int TamanhoMaximoChave = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<RespostaIdempotente<T>> ExecutarAsync<T>(long usuarioId, string? chave, object requisicao,
        Func<Task<(int Status, T Corpo)>> acao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            var (status, corpo) = await acao();
            return new RespostaIdempotente<T>(status, corpo, false);
        }

        var chaveNormalizada = chave.Trim();
        if (chaveNormalizada.Length > TamanhoMaximoChave)
            throw RegraNegocioException.Invalida("Idempotency-Key pode ter no máximo 100 caracteres.");

        var hash = Hash(requisicao);
        if (await BuscarAsync<T>(usuarioId, chaveNormalizada, hash, ct) is { } anterior)
            return anterior;

        try
        {
            return await uow.EmTransacaoAsync(async () =>
            {
                var (status, corpo) = await acao();
                repositorio.Adicionar(new RegistroIdempotencia(usuarioId, chaveNormalizada, hash, status,
                    JsonSerializer.Serialize(corpo, Json), relogio.GetUtcNow().UtcDateTime));
                return new RespostaIdempotente<T>(status, corpo, false);
            }, ct);
        }
        catch (Exception corrida) when (corrida is not RegraNegocioException)
        {
            // Outra requisição com a mesma chave terminou primeiro: devolve a resposta que ela gravou.
            if (await BuscarAsync<T>(usuarioId, chaveNormalizada, hash, ct) is { } gravada)
                return gravada;
            throw;
        }
    }

    private async Task<RespostaIdempotente<T>?> BuscarAsync<T>(long usuarioId, string chave, string hash, CancellationToken ct)
    {
        var registro = await repositorio.BuscarAsync(usuarioId, chave, ct);
        if (registro is null)
            return null;

        if (registro.Expirado(relogio.GetUtcNow().UtcDateTime))
        {
            // Chave vencida (mais de 24 h): libera para um novo uso.
            repositorio.Remover(registro);
            await uow.SalvarAsync(ct);
            return null;
        }

        if (!registro.MesmaRequisicao(hash))
            throw RegraNegocioException.Regra("Esta Idempotency-Key já foi usada com outro conteúdo.");

        return new RespostaIdempotente<T>(registro.StatusHttp, JsonSerializer.Deserialize<T>(registro.CorpoResposta, Json)!, true);
    }

    /// <summary>Apaga as chaves vencidas. Chamado de hora em hora pela manutenção.</summary>
    public Task<int> LimparVencidasAsync(CancellationToken ct = default) =>
        repositorio.ApagarAnterioresAAsync(relogio.GetUtcNow().UtcDateTime - RegistroIdempotencia.Validade, ct);

    /// <summary>SHA-256 do corpo da requisição em JSON: identifica "o mesmo envio".</summary>
    public static string Hash(object requisicao) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(requisicao, requisicao.GetType(), Json))));
}
