using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VeAI.Application.Abstracoes;
using VeAI.Application.Servicos;
using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Eventos;
using VeAI.Infrastructure.Mensageria;

namespace VeAI.Tests.Unit.Infrastructure;

public class MensageriaTests
{
    private static readonly DateTime Agora = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact(DisplayName = "mensagem da fila: JSON com o mesmo formato da API Java, ida e volta")]
    public void Serializacao()
    {
        var evento = EventoRelato.Status(7, 5, StatusRelato.ABERTO, StatusRelato.EM_TRIAGEM, Agora);

        var json = System.Text.Encoding.UTF8.GetString(MensagemEvento.Serializar(evento));
        var volta = MensagemEvento.Desserializar(MensagemEvento.Serializar(evento));

        Assert.Contains("\"tipo\":\"STATUS\"", json);
        Assert.Contains("\"statusNovo\":\"EM_TRIAGEM\"", json);
        Assert.Contains("\"relatoId\":7", json);
        Assert.Equal(evento, volta);
        Assert.Equal("relato.status", volta.RoutingKey);
    }

    [Fact(DisplayName = "routing keys: relato.criado, relato.apoiado, relato.status")]
    public void RoutingKeys()
    {
        Assert.Equal("relato.criado", EventoRelato.Criado(1, 1, Agora).RoutingKey);
        Assert.Equal("relato.apoiado", EventoRelato.Apoiado(1, 1, Agora).RoutingKey);
        Assert.Equal("relato.status", EventoRelato.Status(1, 1, StatusRelato.ABERTO, StatusRelato.EM_TRIAGEM, Agora).RoutingKey);
    }

    [Fact(DisplayName = "consumidor: falha 3 vezes → devolve false (a mensagem vai para a DLQ)")]
    public async Task TresTentativasEDlq()
    {
        var falhas = 0;
        var servicos = new ServiceCollection()
            .AddScoped<NotificacaoServico>(_ =>
            {
                falhas++;
                throw new InvalidOperationException("banco fora");
            })
            .BuildServiceProvider();
        var processador = new ProcessadorEventos(servicos.GetRequiredService<IServiceScopeFactory>(), NullLogger<ProcessadorEventos>.Instance);

        var ok = await processador.ProcessarAsync(EventoRelato.Criado(1, 1, Agora));

        Assert.False(ok);
        Assert.Equal(ProcessadorEventos.Tentativas, falhas);
    }

    [Fact(DisplayName = "mensagens faladas iguais às da API Java")]
    public void Mensagens()
    {
        var relato = Relato.Novo(1, null, new Regiao("Sé", "São Paulo", "SP", null, null), TipoRelato.CALCADA_DANIFICADA,
            Gravidade.ALTA, "Buraco", -23.55, -46.63, null, Agora);

        Assert.Equal("Novo relato de calçada danificada em Sé.", MensagensNotificacao.NovoRelato(relato));
        relato.RegistrarApoio(Agora);
        relato.RegistrarApoio(Agora);
        Assert.Equal("Mais uma pessoa confirmou seu relato de calçada danificada. Agora 2 pessoas já confirmaram.", MensagensNotificacao.Apoio(relato));
        relato.AlterarStatus(StatusRelato.EM_TRIAGEM, null, null, Agora);
        relato.AlterarStatus(StatusRelato.ENCAMINHADO, "SP156-2026-0099999", null, Agora);
        relato.AlterarStatus(StatusRelato.RESOLVIDO, null, null, Agora);
        Assert.Equal("Boa notícia! Seu relato de calçada danificada foi resolvido.", MensagensNotificacao.Status(relato, StatusRelato.RESOLVIDO, true));
        Assert.Equal("O relato de calçada danificada que você apoiou foi encaminhado ao órgão responsável. Protocolo SP156-2026-0099999.",
            MensagensNotificacao.Status(relato, StatusRelato.ENCAMINHADO, false));
    }
}
