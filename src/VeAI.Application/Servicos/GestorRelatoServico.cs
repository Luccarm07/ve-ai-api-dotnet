using VeAI.Application.Abstracoes;
using VeAI.Application.Dtos;
using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Eventos;
using VeAI.Domain.Excecoes;

namespace VeAI.Application.Servicos;

/// <summary>Relatos do ponto de vista do gestor: fila por prioridade, mudança de status e histórico.</summary>
public sealed class GestorRelatoServico(
    IRelatoRepositorio relatos, IAtualizacaoRepositorio atualizacoes, IUsuarioRepositorio usuarios,
    IUnidadeDeTrabalho uow, IPublicadorEventos eventos, TimeProvider relogio)
{
    /// <summary>Fila só com as regiões do gestor, da maior prioridade para a menor.</summary>
    public async Task<PaginaResposta<RelatoResposta>> FilaAsync(long gestorId, StatusRelato? status, long? regiaoId,
                                                                 int? page, int? size, CancellationToken ct = default)
    {
        var gestor = await GestorAsync(gestorId, ct);
        IReadOnlyCollection<long> regioes;
        if (regiaoId is { } id)
        {
            ExigirRegiao(gestor, id);
            regioes = [id];
        }
        else
        {
            regioes = gestor.Regioes.Select(r => r.Id).ToList();
        }

        var p = Paginacao.De(page, size);
        var pagina = regioes.Count == 0
            ? new Pagina<Relato>([], 0)
            : await relatos.FilaAsync(regioes, status, p.Pagina, p.Tamanho, ct);
        return PaginaResposta<RelatoResposta>.De(pagina, p, RelatoResposta.De);
    }

    public async Task<RelatoResposta> BuscarAsync(long gestorId, long relatoId, CancellationToken ct = default)
    {
        var relato = await relatos.BuscarAsync(relatoId, ct) ?? throw NaoEncontrado();
        ExigirRegiao(await GestorAsync(gestorId, ct), relato.RegiaoId);
        return RelatoResposta.De(relato);
    }

    /// <summary>
    /// Valida a transição (máquina de estados), grava o histórico e, depois do commit, publica relato.status:
    /// o consumidor do RabbitMQ notifica o autor e os apoiadores.
    /// </summary>
    public Task<RelatoResposta> AtualizarStatusAsync(long gestorId, long relatoId, StatusRequest req, CancellationToken ct = default) =>
        uow.EmTransacaoAsync(async () =>
        {
            var relato = await relatos.BuscarParaAtualizarAsync(relatoId, ct) ?? throw NaoEncontrado();
            ExigirRegiao(await GestorAsync(gestorId, ct), relato.RegiaoId);

            var agora = relogio.GetUtcNow().UtcDateTime;
            var novo = req.Status!.Value;
            var anterior = relato.AlterarStatus(novo, req.ProtocoloOrgao, req.MotivoDescarte, agora);
            atualizacoes.Adicionar(new AtualizacaoRelato(relato.Id, gestorId, anterior, novo, req.Comentario, agora));

            var evento = EventoRelato.Status(relato.Id, gestorId, anterior, novo, agora);
            uow.AposConfirmar(() => eventos.PublicarAsync(evento));
            return RelatoResposta.De(relato);
        }, ct);

    public async Task<IReadOnlyList<AtualizacaoResposta>> HistoricoAsync(long gestorId, long relatoId, CancellationToken ct = default)
    {
        var relato = await relatos.BuscarAsync(relatoId, ct) ?? throw NaoEncontrado();
        ExigirRegiao(await GestorAsync(gestorId, ct), relato.RegiaoId);
        return (await atualizacoes.HistoricoAsync(relatoId, ct)).Select(AtualizacaoResposta.De).ToList();
    }

    private async Task<Usuario> GestorAsync(long gestorId, CancellationToken ct) =>
        await usuarios.BuscarComRegioesAsync(gestorId, ct) ?? throw RegraNegocioException.Proibido("Gestor não encontrado.");

    private static void ExigirRegiao(Usuario gestor, long regiaoId)
    {
        if (!gestor.Atende(regiaoId))
            throw RegraNegocioException.Proibido("Este relato é de uma região que você não atende.");
    }

    private static RegraNegocioException NaoEncontrado() => RegraNegocioException.NaoEncontrado("Relato não encontrado.");
}

/// <summary>"Dias em aberto" mudam todo dia: recalcula a prioridade dos relatos não finalizados.</summary>
public sealed class RecalculoPrioridadeServico(IRelatoRepositorio relatos, IUnidadeDeTrabalho uow, TimeProvider relogio)
{
    public async Task<int> RecalcularAsync(CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var abertos = await relatos.NaoFinalizadosAsync(ct);
        foreach (var relato in abertos)
            relato.RecalcularPrioridade(agora);
        await uow.SalvarAsync(ct);
        return abertos.Count;
    }
}
