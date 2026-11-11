using VeAI.Domain.Enums;

namespace VeAI.Domain.Regras;

/// <summary>
/// Prioridade na fila do gestor (mesma regra da fn_prioridade_relato do Oracle):
/// peso da gravidade (1/3/5) × (1 + total de apoios) + dias completos em aberto. Status final = 0.
/// </summary>
public static class CalculadoraPrioridade
{
    public static int Calcular(Gravidade gravidade, int totalApoios, StatusRelato status, DateTime criadoEm, DateTime agora)
    {
        if (status.EhFinal())
            return 0;

        var diasEmAberto = Math.Max(0, (int)Math.Floor((agora - criadoEm).TotalDays));
        return gravidade.Peso() * (1 + Math.Max(0, totalApoios)) + diasEmAberto;
    }
}
