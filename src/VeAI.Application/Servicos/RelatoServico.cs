using VeAI.Application.Abstracoes;
using VeAI.Application.Dtos;
using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Eventos;
using VeAI.Domain.Excecoes;
using VeAI.Domain.Regras;

namespace VeAI.Application.Servicos;

/// <summary>Relatos do ponto de vista do cidadão: registrar (com a regra dos 30 m), acompanhar, editar e excluir.</summary>
public sealed class RelatoServico(
    IRelatoRepositorio relatos, IApoioRepositorio apoios, IRegiaoRepositorio regioes, IDescricaoRepositorio descricoes,
    IUnidadeDeTrabalho uow, IPublicadorEventos eventos, TimeProvider relogio)
{
    /// <summary>
    /// Regra dos 30 m: se já existe um relato não finalizado do mesmo tipo a até 30 m, registra +1 apoio
    /// em vez de criar outro. O autor, ou quem já apoiou, recebe JA_REGISTRADO, sem duplicar nada.
    /// </summary>
    public Task<RegistroRelatoResposta> RegistrarAsync(long usuarioId, RelatoRequest req, CancellationToken ct = default) =>
        uow.EmTransacaoAsync(async () =>
        {
            var agora = Agora();
            var (tipo, lat, lon) = (req.Tipo!.Value, req.Latitude!.Value, req.Longitude!.Value);

            var existente = await BuscarProximoAsync(tipo, lat, lon, ct);
            if (existente is not null)
            {
                if (existente.EhDoAutor(usuarioId) || await apoios.ExisteAsync(existente.Id, usuarioId, ct))
                    return Resposta(ResultadoRegistro.JA_REGISTRADO, existente);

                // Ordem importa no Oracle: o INSERT do apoio dispara o trg_conta_apoio (+1 no banco) e só depois
                // o UPDATE grava o mesmo total calculado aqui. Assim o apoio nunca é contado duas vezes.
                apoios.Adicionar(new ApoioRelato(existente.Id, usuarioId, agora));
                await uow.SalvarAsync(ct);
                existente.RegistrarApoio(agora);
                Publicar(EventoRelato.Apoiado(existente.Id, usuarioId, agora));
                return Resposta(ResultadoRegistro.APOIO_REGISTRADO, existente);
            }

            var regiao = req.RegiaoId is { } regiaoId
                ? await regioes.BuscarAsync(regiaoId, ct) ?? throw RegraNegocioException.NaoEncontrado("Região não encontrada.")
                : Regiao.MaisProxima(await regioes.ListarAsync(ct), lat, lon)
                  ?? throw RegraNegocioException.Regra("Nenhuma região de atendimento cadastrada.");

            if (req.DescricaoId is { } descricaoId && await descricoes.BuscarDoUsuarioAsync(descricaoId, usuarioId, ct) is null)
                throw RegraNegocioException.NaoEncontrado("Descrição de origem não encontrada.");

            var novo = Relato.Novo(usuarioId, req.DescricaoId, regiao, tipo, req.Gravidade!.Value, req.Texto!, lat, lon,
                req.Endereco, agora);
            relatos.Adicionar(novo);
            await uow.SalvarAsync(ct);
            Publicar(EventoRelato.Criado(novo.Id, usuarioId, agora));
            return Resposta(ResultadoRegistro.CRIADO, novo);
        }, ct);

    /// <summary>Pré-filtro por "caixa" no banco e distância exata (Haversine) aqui; devolve o mais próximo.</summary>
    private async Task<Relato?> BuscarProximoAsync(TipoRelato tipo, double lat, double lon, CancellationToken ct)
    {
        var dLat = Geo.MargemLatitude(Geo.RaioDeduplicacaoMetros);
        var dLon = Geo.MargemLongitude(Geo.RaioDeduplicacaoMetros, lat);
        var candidatos = await relatos.CandidatosProximosAsync(tipo, lat - dLat, lat + dLat, lon - dLon, lon + dLon, ct);
        return candidatos
            .Where(r => r.AbsorveNovoRelato(tipo, lat, lon))
            .OrderBy(r => r.DistanciaAte(lat, lon))
            .FirstOrDefault();
    }

    public async Task<PaginaResposta<RelatoResposta>> MeusAsync(long usuarioId, int? page, int? size, CancellationToken ct = default)
    {
        var p = Paginacao.De(page, size);
        var pagina = await relatos.DoCidadaoAsync(usuarioId, p.Pagina, p.Tamanho, ct);
        return PaginaResposta<RelatoResposta>.De(pagina, p, RelatoResposta.De);
    }

    /// <summary>O autor e quem apoiou podem acompanhar o relato. Para os demais, ele "não existe" (404).</summary>
    public async Task<RelatoResposta> BuscarAsync(long usuarioId, long id, CancellationToken ct = default)
    {
        var relato = await relatos.BuscarAsync(id, ct) ?? throw NaoEncontrado();
        if (!relato.EhDoAutor(usuarioId) && !await apoios.ExisteAsync(id, usuarioId, ct))
            throw NaoEncontrado();
        return RelatoResposta.De(relato);
    }

    public Task<RelatoResposta> EditarAsync(long usuarioId, long id, RelatoEdicaoRequest req, CancellationToken ct = default) =>
        uow.EmTransacaoAsync(async () =>
        {
            var relato = await DoAutorAsync(usuarioId, id, ct);
            relato.Editar(req.Tipo!.Value, req.Gravidade!.Value, req.Texto!, req.Endereco, Agora());
            return RelatoResposta.De(relato);
        }, ct);

    /// <summary>Só enquanto ABERTO (422) e sem apoios de outras pessoas (409).</summary>
    public Task ExcluirAsync(long usuarioId, long id, CancellationToken ct = default) =>
        uow.EmTransacaoAsync(async () =>
        {
            var relato = await DoAutorAsync(usuarioId, id, ct);
            relato.GarantirQuePodeExcluir();
            relatos.Remover(relato);
            return true;
        }, ct);

    private async Task<Relato> DoAutorAsync(long usuarioId, long id, CancellationToken ct)
    {
        var relato = await relatos.BuscarParaAtualizarAsync(id, ct) ?? throw NaoEncontrado();
        return relato.EhDoAutor(usuarioId) ? relato : throw NaoEncontrado();
    }

    private void Publicar(EventoRelato evento) => uow.AposConfirmar(() => eventos.PublicarAsync(evento));

    private DateTime Agora() => relogio.GetUtcNow().UtcDateTime;

    private static RegistroRelatoResposta Resposta(ResultadoRegistro resultado, Relato r) =>
        new(resultado, resultado == ResultadoRegistro.APOIO_REGISTRADO, RelatoResposta.De(r));

    private static RegraNegocioException NaoEncontrado() => RegraNegocioException.NaoEncontrado("Relato não encontrado.");
}
