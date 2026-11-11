namespace VeAI.Domain.Enums;

/// <summary>Perfil do usuário. Vira a claim "role" do JWT (CIDADAO ou GESTOR).</summary>
public enum Papel
{
    CIDADAO,
    GESTOR
}

/// <summary>
/// Status do relato. A ordem importa: a máquina de estados avança um passo por vez
/// (ABERTO → EM_TRIAGEM → ENCAMINHADO → RESOLVIDO). DESCARTADO pode vir de qualquer status não final.
/// </summary>
public enum StatusRelato
{
    ABERTO,
    EM_TRIAGEM,
    ENCAMINHADO,
    RESOLVIDO,
    DESCARTADO
}

/// <summary>Gravidade da barreira. O peso (1/3/5) está em <see cref="Regras.GravidadeExtensions"/>.</summary>
public enum Gravidade
{
    BAIXA,
    MEDIA,
    ALTA
}

/// <summary>Tipos de barreira estrutural (mesmos valores do CHECK no Oracle).</summary>
public enum TipoRelato
{
    CALCADA_DANIFICADA,
    OBSTRUCAO_PERMANENTE,
    PISO_TATIL,
    RAMPA,
    SEMAFORO_SEM_SOM,
    OBRA_SEM_SINALIZACAO,
    OUTRO
}

/// <summary>AMBIENTE descreve o entorno (padrão); TEXTO lê o texto da foto (placa, embalagem).</summary>
public enum ModoDescricao
{
    AMBIENTE,
    TEXTO
}
