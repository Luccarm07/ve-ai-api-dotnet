using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using VeAI.Application.Opcoes;
using VeAI.Application.Servicos;
using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Excecoes;
using VeAI.Infrastructure.Ia;

namespace VeAI.Tests.Unit.Application;

public class DescricaoRegrasTests
{
    private static readonly DateTime Agora = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan DoisMinutos = TimeSpan.FromMinutes(2);

    private static Descricao Anterior(ModoDescricao modo = ModoDescricao.AMBIENTE, int segundosAtras = 10) =>
        new(1, modo, "Poste na calçada", "Há um poste à frente.", [], ["poste"], false, null, 100, Agora.AddSeconds(-segundosAtras));

    [Fact(DisplayName = "contexto do modo automático: vale só no AMBIENTE e por até 2 minutos")]
    public void Contexto()
    {
        var ok = DescricaoServico.Contexto(Anterior(), ModoDescricao.AMBIENTE, Agora, DoisMinutos);
        Assert.NotNull(ok);
        Assert.Equal(10, ok.SegundosAtras);
        Assert.Equal("Poste na calçada", ok.Resumo);

        Assert.Null(DescricaoServico.Contexto(null, ModoDescricao.AMBIENTE, Agora, DoisMinutos));
        Assert.Null(DescricaoServico.Contexto(Anterior(), ModoDescricao.TEXTO, Agora, DoisMinutos));
        Assert.Null(DescricaoServico.Contexto(Anterior(ModoDescricao.TEXTO), ModoDescricao.AMBIENTE, Agora, DoisMinutos));
        Assert.Null(DescricaoServico.Contexto(Anterior(segundosAtras: 121), ModoDescricao.AMBIENTE, Agora, DoisMinutos));
    }

    [Fact(DisplayName = "validação da foto: vazia 400, grande 413, formato errado 415, JPEG ok")]
    public void ValidarFoto()
    {
        Assert.Equal(TipoErro.RequisicaoInvalida, Assert.Throws<RegraNegocioException>(() => DescricaoServico.ValidarFoto([])).Tipo);
        Assert.Equal(TipoErro.ArquivoGrande, Assert.Throws<RegraNegocioException>(() => DescricaoServico.ValidarFoto(new byte[5 * 1024 * 1024 + 1])).Tipo);
        Assert.Equal(TipoErro.FormatoNaoSuportado, Assert.Throws<RegraNegocioException>(() => DescricaoServico.ValidarFoto([1, 2, 3, 4])).Tipo);
        Assert.Equal("image/jpeg", DescricaoServico.ValidarFoto([0xFF, 0xD8, 0xFF, 0xE0]).Tipo);
    }

    [Fact(DisplayName = "memória de sugestões: manual sempre sugere; automático não repete em 2 min")]
    public void MemoriaSugestoes()
    {
        var relogio = new FakeTimeProvider(new DateTimeOffset(Agora));
        var memoria = new MemoriaSugestoes(Options.Create(new IaOptions()), relogio);

        Assert.True(memoria.DeveSugerir(1, TipoRelato.RAMPA, automatico: true));
        Assert.False(memoria.DeveSugerir(1, TipoRelato.RAMPA, automatico: true));
        Assert.True(memoria.DeveSugerir(1, TipoRelato.PISO_TATIL, automatico: true)); // outro tipo
        Assert.True(memoria.DeveSugerir(2, TipoRelato.RAMPA, automatico: true));      // outra pessoa
        Assert.True(memoria.DeveSugerir(1, TipoRelato.RAMPA, automatico: false));     // manual

        relogio.Advance(TimeSpan.FromSeconds(119));
        Assert.False(memoria.DeveSugerir(1, TipoRelato.RAMPA, automatico: true));
        relogio.Advance(TimeSpan.FromSeconds(2));
        Assert.True(memoria.DeveSugerir(1, TipoRelato.RAMPA, automatico: true));
    }

    [Fact(DisplayName = "descritor falso: confirmação curta e cenário igual ao Arrays.hashCode do Java")]
    public void DescritorFalsoRegras()
    {
        Assert.Equal("Nada mudou à sua frente.", DescritorFalso.Confirmacao([]));
        Assert.Equal("Poste continua no mesmo lugar, à sua frente.", DescritorFalso.Confirmacao(["poste"]));
        Assert.Equal("Poste, muro e lixeira continuam no mesmo lugar, à sua frente.", DescritorFalso.Confirmacao(["poste", "muro", "lixeira"]));

        // Java: Arrays.hashCode(new byte[]{1, 2, 3}) = 30817 → 30817 mod 4 = 1; com byte negativo também (floorMod).
        Assert.Equal(1, DescritorFalso.CenarioDe([1, 2, 3]));
        Assert.InRange(DescritorFalso.CenarioDe([0xFF, 0xD8, 0xFF]), 0, 3);
        Assert.Equal(DescritorFalso.CenarioDe([9, 8, 7]), DescritorFalso.CenarioDe([9, 8, 7]));
    }
}
