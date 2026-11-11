using VeAI.Domain.Enums;

namespace VeAI.Domain.Regras;

public static class StatusRelatoExtensions
{
    /// <summary>Status que ainda contam para deduplicação (30 m) e para a fila do gestor.</summary>
    public static readonly IReadOnlySet<StatusRelato> NaoFinais =
        new HashSet<StatusRelato> { StatusRelato.ABERTO, StatusRelato.EM_TRIAGEM, StatusRelato.ENCAMINHADO };

    public static bool EhFinal(this StatusRelato status) =>
        status is StatusRelato.RESOLVIDO or StatusRelato.DESCARTADO;

    /// <summary>
    /// Máquina de estados (mesma do prc_atualizar_status no Oracle): avança um passo por vez,
    /// nunca volta, e DESCARTADO é aceito a partir de qualquer status não final.
    /// </summary>
    public static bool PodeIrPara(this StatusRelato atual, StatusRelato novo)
    {
        if (atual.EhFinal() || novo == atual)
            return false;
        if (novo == StatusRelato.DESCARTADO)
            return true;
        return (int)novo == (int)atual + 1;
    }
}

public static class GravidadeExtensions
{
    /// <summary>Peso na fórmula de prioridade: BAIXA 1, MEDIA 3, ALTA 5.</summary>
    public static int Peso(this Gravidade gravidade) => gravidade switch
    {
        Gravidade.BAIXA => 1,
        Gravidade.MEDIA => 3,
        Gravidade.ALTA => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(gravidade))
    };
}

public static class TipoRelatoExtensions
{
    /// <summary>Texto falado nas notificações e na sugestão de relato.</summary>
    public static string Rotulo(this TipoRelato tipo) => tipo switch
    {
        TipoRelato.CALCADA_DANIFICADA => "calçada danificada",
        TipoRelato.OBSTRUCAO_PERMANENTE => "calçada obstruída",
        TipoRelato.PISO_TATIL => "problema no piso tátil",
        TipoRelato.RAMPA => "problema na rampa",
        TipoRelato.SEMAFORO_SEM_SOM => "semáforo sem som",
        TipoRelato.OBRA_SEM_SINALIZACAO => "obra sem sinalização",
        _ => "barreira"
    };
}
