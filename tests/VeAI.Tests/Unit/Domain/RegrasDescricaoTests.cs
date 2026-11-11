using System.Text;
using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Excecoes;
using VeAI.Domain.Modelos;
using VeAI.Domain.Regras;

namespace VeAI.Tests.Unit.Domain;

public class RegrasDescricaoTests
{
    [Fact(DisplayName = "sugere relato só para barreira detectada com confiança mínima")]
    public void SugestaoDeRelato()
    {
        var sugestao = RegraSugestaoRelato.Avaliar(new Barreira(true, TipoRelato.OBSTRUCAO_PERMANENTE, Gravidade.MEDIA, 0.86, "poste"));

        Assert.NotNull(sugestao);
        Assert.Equal(TipoRelato.OBSTRUCAO_PERMANENTE, sugestao.Tipo);
        Assert.Equal("Parece haver calçada obstruída. Quer avisar a rede de apoio?", sugestao.Pergunta);

        Assert.Null(RegraSugestaoRelato.Avaliar(new Barreira(true, TipoRelato.RAMPA, Gravidade.ALTA, 0.69, "?")));
        Assert.Null(RegraSugestaoRelato.Avaliar(Barreira.Nenhuma()));
        Assert.Null(RegraSugestaoRelato.Avaliar(new Barreira(true, null, null, 0.99, "?")));
        Assert.Null(RegraSugestaoRelato.Avaliar(null));
    }

    [Fact(DisplayName = "sem gravidade informada, a sugestão assume MEDIA; OUTRO vira 'uma barreira no caminho'")]
    public void SugestaoPadroes()
    {
        var sugestao = RegraSugestaoRelato.Avaliar(new Barreira(true, TipoRelato.OUTRO, null, 0.9, null));

        Assert.Equal(Gravidade.MEDIA, sugestao!.Gravidade);
        Assert.Equal("Parece haver uma barreira no caminho. Quer avisar a rede de apoio?", sugestao.Pergunta);
    }

    [Fact(DisplayName = "formato da foto é conferido pela assinatura dos bytes")]
    public void AssinaturaDaImagem()
    {
        Assert.Equal("image/jpeg", AssinaturaImagem.Detectar([0xFF, 0xD8, 0xFF, 0]));
        Assert.Equal("image/png", AssinaturaImagem.Detectar([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]));
        Assert.Equal("image/webp", AssinaturaImagem.Detectar(Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8 ")));
        Assert.Null(AssinaturaImagem.Detectar(Encoding.ASCII.GetBytes("GIF89a")));
        Assert.Null(AssinaturaImagem.Detectar([]));
    }

    [Fact(DisplayName = "barreira detectada sem tipo é recusada; avaliação fica registrada")]
    public void Descricao()
    {
        var agora = DateTime.UtcNow;
        Assert.Throws<RegraNegocioException>(() =>
            new Descricao(1, ModoDescricao.AMBIENTE, "Calçada", null, [], [], true, null, 100, agora));

        var descricao = new Descricao(1, ModoDescricao.AMBIENTE, "Calçada livre", null,
            [new Perigo("degrau para baixo", "à frente", "2 metros")], ["poste"], false, TipoRelato.RAMPA, -5, agora);

        Assert.Null(descricao.BarreiraTipo); // sem barreira, o tipo é descartado
        Assert.Equal(0, descricao.LatenciaMs);
        Assert.Null(descricao.Util);
        descricao.Avaliar(true);
        Assert.True(descricao.Util);
        Assert.True(descricao.EhDo(1));
    }
}
