using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Eventos;
using VeAI.Domain.Modelos;

namespace VeAI.Application.Abstracoes;

// Portas da camada de aplicação. A Infrastructure implementa (EF Core, BCrypt, JWT);
// os serviços dependem só destas interfaces, o que permite testá-los sem banco.

public interface IUsuarioRepositorio
{
    Task<Usuario?> BuscarPorEmailAsync(string emailNormalizado, CancellationToken ct = default);
    Task<Usuario?> BuscarComRegioesAsync(long id, CancellationToken ct = default);
    Task<bool> EmailExisteAsync(string emailNormalizado, CancellationToken ct = default);
    Task<IReadOnlyList<long>> IdsDosGestoresDaRegiaoAsync(long regiaoId, CancellationToken ct = default);
    void Adicionar(Usuario usuario);
}

public interface IRegiaoRepositorio
{
    Task<IReadOnlyList<Regiao>> ListarAsync(CancellationToken ct = default);
    Task<Regiao?> BuscarAsync(long id, CancellationToken ct = default);
}

/// <summary>Página de resultados vinda do banco (itens da página + total geral).</summary>
public sealed record Pagina<T>(IReadOnlyList<T> Itens, long Total);

public interface IRelatoRepositorio
{
    /// <summary>Relato com a região carregada (leitura).</summary>
    Task<Relato?> BuscarAsync(long id, CancellationToken ct = default);

    /// <summary>Relato com a região carregada e a linha bloqueada até o fim da transação (Oracle: FOR UPDATE).</summary>
    Task<Relato?> BuscarParaAtualizarAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Pré-filtro da regra dos 30 m: mesmo tipo, não finalizados, dentro de uma "caixa" de lat/long.
    /// As linhas ficam bloqueadas até o fim da transação, para dois apoios simultâneos não se perderem.
    /// </summary>
    Task<IReadOnlyList<Relato>> CandidatosProximosAsync(TipoRelato tipo, double latMin, double latMax,
                                                        double lonMin, double lonMax, CancellationToken ct = default);

    Task<Pagina<Relato>> DoCidadaoAsync(long cidadaoId, int pagina, int tamanho, CancellationToken ct = default);

    /// <summary>Fila do gestor: maior prioridade primeiro, depois o mais antigo.</summary>
    Task<Pagina<Relato>> FilaAsync(IReadOnlyCollection<long> regioes, StatusRelato? status, int pagina, int tamanho,
                                   CancellationToken ct = default);

    Task<IReadOnlyList<Relato>> NaoFinalizadosAsync(CancellationToken ct = default);
    void Adicionar(Relato relato);
    void Remover(Relato relato);
}

public interface IApoioRepositorio
{
    Task<bool> ExisteAsync(long relatoId, long usuarioId, CancellationToken ct = default);
    Task<IReadOnlyList<long>> IdsDosApoiadoresAsync(long relatoId, CancellationToken ct = default);
    void Adicionar(ApoioRelato apoio);
}

public interface IAtualizacaoRepositorio
{
    Task<IReadOnlyList<AtualizacaoRelato>> HistoricoAsync(long relatoId, CancellationToken ct = default);
    void Adicionar(AtualizacaoRelato atualizacao);
}

public interface IDescricaoRepositorio
{
    Task<Descricao?> BuscarDoUsuarioAsync(long id, long usuarioId, CancellationToken ct = default);
    Task<Pagina<Descricao>> DoUsuarioAsync(long usuarioId, int pagina, int tamanho, CancellationToken ct = default);
    void Adicionar(Descricao descricao);
    void Remover(Descricao descricao);
}

/// <summary>
/// Porta para o modelo de visão. Implementações: OpenAI (de verdade) e um descritor falso
/// (respostas fixas, sem custo), escolhidas pela configuração Ia__Provedor.
/// </summary>
public interface IDescritorImagem
{
    string Provedor { get; }

    /// <summary>Descreve a foto. "anterior" vem só no modo automático; com ele, o modelo diz se há novidade.</summary>
    Task<DescricaoIA> DescreverAsync(ImagemRecebida imagem, ModoDescricao modo, ContextoAnterior? anterior, CancellationToken ct = default);

    /// <summary>Pergunta livre por voz sobre a foto. Nada é gravado.</summary>
    Task<string> ResponderAsync(ImagemRecebida imagem, string pergunta, CancellationToken ct = default);
}

public interface IIdempotenciaRepositorio
{
    Task<RegistroIdempotencia?> BuscarAsync(long usuarioId, string chave, CancellationToken ct = default);
    void Adicionar(RegistroIdempotencia registro);
    void Remover(RegistroIdempotencia registro);
    Task<int> ApagarAnterioresAAsync(DateTime limite, CancellationToken ct = default);
}

public interface INotificacaoRepositorio
{
    Task<Pagina<Notificacao>> DoUsuarioAsync(long usuarioId, bool? lida, int pagina, int tamanho, CancellationToken ct = default);
    Task<Notificacao?> BuscarDoUsuarioAsync(long id, long usuarioId, CancellationToken ct = default);
    void Adicionar(Notificacao notificacao);
}

/// <summary>Publica eventos de relato (RabbitMQ). Chamado só depois do commit.</summary>
public interface IPublicadorEventos
{
    Task PublicarAsync(EventoRelato evento, CancellationToken ct = default);
}

/// <summary>Controla gravação e transação (SaveChanges / BeginTransaction do EF Core).</summary>
public interface IUnidadeDeTrabalho
{
    /// <summary>Grava as alterações pendentes.</summary>
    Task SalvarAsync(CancellationToken ct = default);

    /// <summary>
    /// Executa a ação numa transação: grava e confirma no fim, ou desfaz tudo se algo falhar.
    /// Se já existe uma transação aberta, a ação participa dela.
    /// </summary>
    Task<T> EmTransacaoAsync<T>(Func<Task<T>> acao, CancellationToken ct = default);

    /// <summary>Agenda uma ação para depois do commit (ex.: publicar evento). Se houver rollback, é descartada.</summary>
    void AposConfirmar(Func<Task> acao);
}

public interface ISenhaHasher
{
    string Gerar(string senha);
    bool Confere(string senha, string hash);
}

public sealed record TokenEmitido(string Token, DateTime ExpiraEm);

public interface IGeradorToken
{
    TokenEmitido Emitir(Usuario usuario);
}
