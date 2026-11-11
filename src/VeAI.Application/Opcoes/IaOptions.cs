namespace VeAI.Application.Opcoes;

/// <summary>Configuração da IA (seção "Ia"). A chave da OpenAI vem só do .env (OPENAI_API_KEY).</summary>
public sealed class IaOptions
{
    public const string Secao = "Ia";

    /// <summary>"falso" (padrão, sem custo) ou "openai".</summary>
    public string Provedor { get; set; } = "falso";
    public string Modelo { get; set; } = "gpt-6-luna";
    public string BaseUrl { get; set; } = "https://api.openai.com";
    /// <summary>Preenchida a partir de OPENAI_API_KEY (só no .env). Nunca vai para log nem resposta.</summary>
    public string? ApiKey { get; set; }
    /// <summary>Opcional, para modelos de raciocínio: minimal, low, medium ou high.</summary>
    public string? ReasoningEffort { get; set; }
    public int TimeoutSegundos { get; set; } = 30;

    /// <summary>Confiança mínima (0 a 1) para sugerir um relato.</summary>
    public double ConfiancaMinima { get; set; } = 0.7;
    /// <summary>Modo automático: a descrição anterior só serve de referência por este tempo.</summary>
    public int ValidadeContextoSegundos { get; set; } = 120;
    /// <summary>A mesma sugestão de relato (mesmo tipo) não se repete neste intervalo no modo automático.</summary>
    public int JanelaSugestaoSegundos { get; set; } = 120;

    public bool OpenAi => string.Equals(Provedor, "openai", StringComparison.OrdinalIgnoreCase);
}
