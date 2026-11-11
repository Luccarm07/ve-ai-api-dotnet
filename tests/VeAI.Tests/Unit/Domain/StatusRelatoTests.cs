using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Excecoes;
using VeAI.Domain.Regras;
using static VeAI.Domain.Enums.StatusRelato;

namespace VeAI.Tests.Unit.Domain;

public class StatusRelatoTests
{
    private static readonly DateTime Agora = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    [Theory(DisplayName = "transições válidas")]
    [InlineData(ABERTO, EM_TRIAGEM)]
    [InlineData(EM_TRIAGEM, ENCAMINHADO)]
    [InlineData(ENCAMINHADO, RESOLVIDO)]
    [InlineData(ABERTO, DESCARTADO)]
    [InlineData(EM_TRIAGEM, DESCARTADO)]
    [InlineData(ENCAMINHADO, DESCARTADO)]
    public void Validas(StatusRelato de, StatusRelato para) => Assert.True(de.PodeIrPara(para));

    [Theory(DisplayName = "transições inválidas: pular etapa, voltar, sair de status final, repetir")]
    [InlineData(ABERTO, ENCAMINHADO)]
    [InlineData(ABERTO, RESOLVIDO)]
    [InlineData(EM_TRIAGEM, RESOLVIDO)]
    [InlineData(EM_TRIAGEM, ABERTO)]
    [InlineData(ENCAMINHADO, EM_TRIAGEM)]
    [InlineData(RESOLVIDO, ABERTO)]
    [InlineData(RESOLVIDO, DESCARTADO)]
    [InlineData(DESCARTADO, ABERTO)]
    [InlineData(ABERTO, ABERTO)]
    public void Invalidas(StatusRelato de, StatusRelato para) => Assert.False(de.PodeIrPara(para));

    [Fact(DisplayName = "transição inválida na entidade gera erro de regra (422)")]
    public void TransicaoInvalidaNaEntidade()
    {
        var relato = NovoRelato();

        var erro = Assert.Throws<RegraNegocioException>(() => relato.AlterarStatus(RESOLVIDO, "P-1", null, Agora));

        Assert.Equal(TipoErro.RegraViolada, erro.Tipo);
        Assert.Equal(ABERTO, relato.Status);
    }

    [Fact(DisplayName = "ENCAMINHADO exige protocolo e DESCARTADO exige motivo")]
    public void Exigencias()
    {
        var relato = NovoRelato();
        relato.AlterarStatus(EM_TRIAGEM, null, null, Agora);

        Assert.Contains("protocolo", Assert.Throws<RegraNegocioException>(() => relato.AlterarStatus(ENCAMINHADO, "  ", null, Agora)).Message);
        Assert.Contains("motivo", Assert.Throws<RegraNegocioException>(() => relato.AlterarStatus(DESCARTADO, null, null, Agora)).Message);

        Assert.Equal(EM_TRIAGEM, relato.AlterarStatus(ENCAMINHADO, " SP156-1 ", null, Agora));
        Assert.Equal("SP156-1", relato.ProtocoloOrgao);
    }

    [Fact(DisplayName = "finalizar zera a prioridade; editar só enquanto ABERTO")]
    public void FinalizarEEditar()
    {
        var relato = NovoRelato();
        Assert.Equal(5, relato.Prioridade);

        relato.RegistrarApoio(Agora);
        Assert.Equal(10, relato.Prioridade);

        relato.AlterarStatus(DESCARTADO, null, "Não encontrado na vistoria", Agora);
        Assert.Equal(0, relato.Prioridade);
        Assert.Contains("aberto", Assert.Throws<RegraNegocioException>(
            () => relato.Editar(TipoRelato.RAMPA, Gravidade.BAIXA, "x", null, Agora)).Message);

        Assert.Equal(DESCARTADO, relato.Status);
        Assert.False(ABERTO.EhFinal());
        Assert.True(RESOLVIDO.EhFinal());
    }

    internal static Relato NovoRelato(TipoRelato tipo = TipoRelato.CALCADA_DANIFICADA, double lat = -23.55, double lon = -46.63) =>
        Relato.Novo(1, null, new Regiao("Sé", "São Paulo", "SP", -23.55, -46.63), tipo, Gravidade.ALTA,
            "Buraco grande na calçada", lat, lon, null, Agora);
}
