using VeAI.Domain.Enums;
using VeAI.Domain.Excecoes;

namespace VeAI.Tests.Unit.Domain;

public class RelatoTests
{
    private static readonly DateTime Agora = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    // Relato #1 do seed (Rua dos Pinheiros) e um ponto a ~10 m dele.
    private const double Lat = -23.56612, Lon = -46.68431;

    [Fact(DisplayName = "regra dos 30 m: mesmo tipo a ~10 m vira apoio")]
    public void MesmoTipoPertoViraApoio()
    {
        var relato = StatusRelatoTests.NovoRelato(TipoRelato.CALCADA_DANIFICADA, Lat, Lon);

        Assert.True(relato.AbsorveNovoRelato(TipoRelato.CALCADA_DANIFICADA, -23.56620, -46.68436));
    }

    [Fact(DisplayName = "regra dos 30 m: a ~35 m já é um relato novo")]
    public void ForaDoRaioCriaNovo()
    {
        var relato = StatusRelatoTests.NovoRelato(TipoRelato.CALCADA_DANIFICADA, Lat, Lon);
        var lat35m = Lat + 35 / 111_320d;

        Assert.False(relato.AbsorveNovoRelato(TipoRelato.CALCADA_DANIFICADA, lat35m, Lon));
    }

    [Fact(DisplayName = "regra dos 30 m: tipo diferente no mesmo lugar não vira apoio")]
    public void TipoDiferenteCriaNovo()
    {
        var relato = StatusRelatoTests.NovoRelato(TipoRelato.CALCADA_DANIFICADA, Lat, Lon);

        Assert.False(relato.AbsorveNovoRelato(TipoRelato.RAMPA, Lat, Lon));
    }

    [Fact(DisplayName = "regra dos 30 m: relato finalizado não recebe apoio")]
    public void FinalizadoNaoAbsorve()
    {
        var relato = StatusRelatoTests.NovoRelato(TipoRelato.CALCADA_DANIFICADA, Lat, Lon);
        relato.AlterarStatus(StatusRelato.DESCARTADO, null, "Duplicado", Agora);

        Assert.False(relato.AbsorveNovoRelato(TipoRelato.CALCADA_DANIFICADA, Lat, Lon));
    }

    [Fact(DisplayName = "excluir relato com apoios gera conflito (409)")]
    public void ExcluirComApoios()
    {
        var relato = StatusRelatoTests.NovoRelato();
        relato.GarantirQuePodeExcluir(); // sem apoios: pode

        relato.RegistrarApoio(Agora);

        Assert.Equal(TipoErro.Conflito, Assert.Throws<RegraNegocioException>(relato.GarantirQuePodeExcluir).Tipo);
    }

    [Fact(DisplayName = "relato novo nasce ABERTO, sem apoios, com textos aparados")]
    public void NovoRelato()
    {
        var relato = StatusRelatoTests.NovoRelato();

        Assert.Equal(StatusRelato.ABERTO, relato.Status);
        Assert.Equal(0, relato.TotalApoios);
        Assert.True(relato.EhDoAutor(1));
        Assert.False(relato.EhDoAutor(2));
        Assert.Equal(Agora, relato.CriadoEm);
    }
}
