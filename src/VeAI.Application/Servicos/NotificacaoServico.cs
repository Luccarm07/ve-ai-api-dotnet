using VeAI.Application.Abstracoes;
using VeAI.Application.Dtos;
using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Eventos;
using VeAI.Domain.Excecoes;
using VeAI.Domain.Regras;

namespace VeAI.Application.Servicos;

public sealed record NotificacaoResposta(long Id, long RelatoId, string Mensagem, bool Lida, DateTime CriadoEm)
{
    public static NotificacaoResposta De(Notificacao n) => new(n.Id, n.RelatoId, n.Mensagem, n.Lida, n.CriadoEm);
}

/// <summary>Notificações do usuário e a geração delas a partir dos eventos que chegam pelo RabbitMQ.</summary>
public sealed class NotificacaoServico(
    INotificacaoRepositorio notificacoes, IRelatoRepositorio relatos, IApoioRepositorio apoios, IUsuarioRepositorio usuarios,
    IUnidadeDeTrabalho uow, TimeProvider relogio)
{
    public async Task<PaginaResposta<NotificacaoResposta>> ListarAsync(long usuarioId, bool? lida, int? page, int? size,
                                                                       CancellationToken ct = default)
    {
        var p = Paginacao.De(page, size);
        var pagina = await notificacoes.DoUsuarioAsync(usuarioId, lida, p.Pagina, p.Tamanho, ct);
        return PaginaResposta<NotificacaoResposta>.De(pagina, p, NotificacaoResposta.De);
    }

    public async Task<NotificacaoResposta> MarcarComoLidaAsync(long usuarioId, long id, CancellationToken ct = default)
    {
        var notificacao = await notificacoes.BuscarDoUsuarioAsync(id, usuarioId, ct)
                          ?? throw RegraNegocioException.NaoEncontrado("Notificação não encontrada.");
        notificacao.MarcarComoLida();
        await uow.SalvarAsync(ct);
        return NotificacaoResposta.De(notificacao);
    }

    /// <summary>
    /// Chamado pelo consumidor da fila. Destinatários: relato.criado → gestores da região;
    /// relato.apoiado → autor; relato.status → autor e apoiadores.
    /// O relato é bloqueado antes de gravar (mesma ordem da exclusão), para não disputar com um DELETE do autor:
    /// se a exclusão vence, o relato some e o evento é ignorado.
    /// </summary>
    public Task<int> GerarAsync(EventoRelato evento, CancellationToken ct = default) =>
        uow.EmTransacaoAsync(async () =>
        {
            var relato = await relatos.BuscarParaAtualizarAsync(evento.RelatoId, ct);
            if (relato is null)
                return 0;

            var agora = relogio.GetUtcNow().UtcDateTime;
            var criadas = 0;
            void Notificar(long usuarioId, string mensagem)
            {
                notificacoes.Adicionar(new Notificacao(usuarioId, relato.Id, mensagem, agora));
                criadas++;
            }

            switch (evento.Tipo)
            {
                case TipoEvento.CRIADO:
                    foreach (var gestorId in await usuarios.IdsDosGestoresDaRegiaoAsync(relato.RegiaoId, ct))
                        Notificar(gestorId, MensagensNotificacao.NovoRelato(relato));
                    break;
                case TipoEvento.APOIADO:
                    Notificar(relato.CidadaoId, MensagensNotificacao.Apoio(relato));
                    break;
                case TipoEvento.STATUS:
                    var novo = evento.StatusNovo ?? relato.Status;
                    Notificar(relato.CidadaoId, MensagensNotificacao.Status(relato, novo, paraAutor: true));
                    foreach (var apoiadorId in (await apoios.IdsDosApoiadoresAsync(relato.Id, ct)).Where(id => id != relato.CidadaoId).Distinct())
                        Notificar(apoiadorId, MensagensNotificacao.Status(relato, novo, paraAutor: false));
                    break;
            }
            return criadas;
        }, ct);
}

/// <summary>Textos curtos, escritos para serem lidos em voz alta pelo app (iguais aos da API Java).</summary>
public static class MensagensNotificacao
{
    public static string NovoRelato(Relato r) => $"Novo relato de {r.Tipo.Rotulo()} em {r.Regiao?.Nome}.";

    public static string Apoio(Relato r)
    {
        var pessoas = r.TotalApoios == 1 ? "1 pessoa já confirmou" : $"{r.TotalApoios} pessoas já confirmaram";
        return $"Mais uma pessoa confirmou seu relato de {r.Tipo.Rotulo()}. Agora {pessoas}.";
    }

    /// <summary>paraAutor muda o sujeito: "Seu relato" × "O relato que você apoiou".</summary>
    public static string Status(Relato r, StatusRelato novo, bool paraAutor)
    {
        var sujeito = paraAutor ? $"Seu relato de {r.Tipo.Rotulo()}" : $"O relato de {r.Tipo.Rotulo()} que você apoiou";
        return novo switch
        {
            StatusRelato.EM_TRIAGEM => $"{sujeito} está em análise pela rede de apoio.",
            StatusRelato.ENCAMINHADO => $"{sujeito} foi encaminhado ao órgão responsável. Protocolo {r.ProtocoloOrgao}.",
            StatusRelato.RESOLVIDO => $"Boa notícia! {sujeito} foi resolvido.",
            StatusRelato.DESCARTADO => $"{sujeito} foi encerrado. Motivo: {r.MotivoDescarte}",
            _ => $"{sujeito} foi reaberto."
        };
    }
}
