namespace VeAI.Domain.Excecoes;

/// <summary>Categoria do erro. A camada de API traduz cada uma para um código HTTP.</summary>
public enum TipoErro
{
    /// <summary>Regra de negócio violada (422). Ex.: transição de status inválida.</summary>
    RegraViolada,
    /// <summary>Recurso inexistente ou de outro usuário (404).</summary>
    NaoEncontrado,
    /// <summary>Conflito com o estado atual (409). Ex.: excluir relato que já tem apoios.</summary>
    Conflito,
    /// <summary>Usuário autenticado sem permissão sobre o recurso (403).</summary>
    Proibido,
    /// <summary>Requisição malformada (400).</summary>
    RequisicaoInvalida,
    /// <summary>Credenciais inválidas ou ausentes (401).</summary>
    NaoAutenticado,
    /// <summary>Arquivo grande demais (413).</summary>
    ArquivoGrande,
    /// <summary>Formato de arquivo não suportado (415).</summary>
    FormatoNaoSuportado,
    /// <summary>Serviço externo (IA) falhou ou não respondeu (502).</summary>
    ServicoExterno
}

/// <summary>Erro de negócio com mensagem curta, pronta para ser lida em voz alta pelo app.</summary>
public class RegraNegocioException(TipoErro tipo, string mensagem) : Exception(mensagem)
{
    public TipoErro Tipo { get; } = tipo;

    public static RegraNegocioException Regra(string mensagem) => new(TipoErro.RegraViolada, mensagem);
    public static RegraNegocioException NaoEncontrado(string mensagem) => new(TipoErro.NaoEncontrado, mensagem);
    public static RegraNegocioException Conflito(string mensagem) => new(TipoErro.Conflito, mensagem);
    public static RegraNegocioException Proibido(string mensagem) => new(TipoErro.Proibido, mensagem);
    public static RegraNegocioException Invalida(string mensagem) => new(TipoErro.RequisicaoInvalida, mensagem);
}
