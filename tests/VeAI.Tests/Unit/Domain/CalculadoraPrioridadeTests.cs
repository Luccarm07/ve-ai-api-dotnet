using VeAI.Domain.Enums;
using VeAI.Domain.Regras;

namespace VeAI.Tests.Unit.Domain;

public class CalculadoraPrioridadeTests
{
    private static readonly DateTime Agora = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    [Theory(DisplayName = "peso da gravidade × (1 + apoios) + dias em aberto")]
    [InlineData(Gravidade.BAIXA, 0, 0, 1)]
    [InlineData(Gravidade.MEDIA, 0, 0, 3)]
    [InlineData(Gravidade.ALTA, 0, 0, 5)]
    [InlineData(Gravidade.ALTA, 2, 60, 75)] // relato #1 do seed: 5 × (1 + 2) + 60
    [InlineData(Gravidade.MEDIA, 1, 50, 56)]
    [InlineData(Gravidade.BAIXA, 4, 10, 15)]
    public void Formula(Gravidade gravidade, int apoios, int dias, int esperado)
    {
        var criadoEm = Agora.AddDays(-dias);

        Assert.Equal(esperado, CalculadoraPrioridade.Calcular(gravidade, apoios, StatusRelato.ABERTO, criadoEm, Agora));
    }

    [Theory(DisplayName = "relato finalizado sai da fila: prioridade 0")]
    [InlineData(StatusRelato.RESOLVIDO)]
    [InlineData(StatusRelato.DESCARTADO)]
    public void FinalizadoValeZero(StatusRelato status) =>
        Assert.Equal(0, CalculadoraPrioridade.Calcular(Gravidade.ALTA, 10, status, Agora.AddDays(-90), Agora));

    [Fact(DisplayName = "dias incompletos não contam e data no futuro não gera valor negativo")]
    public void DiasArredondadosParaBaixo()
    {
        Assert.Equal(3 + 1, CalculadoraPrioridade.Calcular(Gravidade.MEDIA, 0, StatusRelato.EM_TRIAGEM, Agora.AddHours(-47), Agora));
        Assert.Equal(3, CalculadoraPrioridade.Calcular(Gravidade.MEDIA, 0, StatusRelato.ABERTO, Agora.AddHours(1), Agora));
    }

    [Fact(DisplayName = "pesos 1/3/5 iguais aos do Oracle")]
    public void Pesos()
    {
        Assert.Equal(1, Gravidade.BAIXA.Peso());
        Assert.Equal(3, Gravidade.MEDIA.Peso());
        Assert.Equal(5, Gravidade.ALTA.Peso());
    }
}
