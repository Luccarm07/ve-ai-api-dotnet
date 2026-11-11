using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using VeAI.Application.Abstracoes;
using VeAI.Application.Dtos;
using VeAI.Application.Opcoes;
using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Excecoes;
using VeAI.Domain.Modelos;
using VeAI.Domain.Regras;

namespace VeAI.Application.Servicos;

/// <summary>Descrição por IA: valida a foto, chama o descritor, grava só o texto e decide se sugere um relato.</summary>
public sealed class DescricaoServico(
    IDescricaoRepositorio descricoes, IUsuarioRepositorio usuarios, IUnidadeDeTrabalho uow, IDescritorImagem descritor,
    MemoriaSugestoes sugestoes, IOptions<IaOptions> opcoes, TimeProvider relogio)
{
    private const int TamanhoResumo = 500;
    private const int TamanhoCompleta = 4000;
    private const int TamanhoPergunta = 500;

    /// <summary>
    /// Modo manual: só a foto, descrição completa. Modo automático: com anteriorId, a IA compara com a foto anterior
    /// (até 2 min) e fala só o que mudou, ou confirma em uma frase. A foto nunca é gravada.
    /// </summary>
    public async Task<DescricaoResposta> DescreverAsync(long usuarioId, ModoDescricao modo, byte[] foto, long? anteriorId,
                                                         CancellationToken ct = default)
    {
        var imagem = ValidarFoto(foto);
        var anterior = anteriorId is { } id
            ? await DaPessoaAsync(usuarioId, id, ct, "Descrição anterior não encontrada.")
            : null;

        var contexto = Contexto(anterior, modo, Agora(), TimeSpan.FromSeconds(opcoes.Value.ValidadeContextoSegundos));
        var cronometro = Stopwatch.StartNew();
        var ia = (await descritor.DescreverAsync(imagem, modo, contexto, ct)).Normalizada();
        var latenciaMs = (int)cronometro.ElapsedMilliseconds;

        // Rede de segurança: se antes não havia perigo e agora há, a pessoa precisa ouvir, diga a IA o que disser.
        var novidade = contexto is null || ia.Novidade == true || (anterior!.Perigos.Count == 0 && ia.Perigos!.Count > 0);
        var completa = modo == ModoDescricao.TEXTO && !string.IsNullOrWhiteSpace(ia.TextoLido) ? ia.TextoLido : ia.DescricaoCompleta;
        var barreira = ia.Barreira!;
        var temBarreira = barreira.Detectada && barreira.Tipo is not null;

        var descricao = new Descricao(usuarioId, modo, Limitar(ia.Resumo, TamanhoResumo), Limitar(completa, TamanhoCompleta),
            ia.Perigos!, ia.Objetos!, temBarreira, temBarreira ? barreira.Tipo : null, latenciaMs, Agora());
        descricoes.Adicionar(descricao);
        await uow.SalvarAsync(ct);

        var sugestao = RegraSugestaoRelato.Avaliar(barreira, opcoes.Value.ConfiancaMinima);
        if (sugestao is not null && !sugestoes.DeveSugerir(usuarioId, sugestao.Tipo, automatico: contexto is not null))
            sugestao = null;

        return DescricaoResposta.De(descricao, novidade, sugestao);
    }

    /// <summary>
    /// A descrição anterior só vale como referência no modo AMBIENTE e por pouco tempo: depois disso a pessoa
    /// pode ter parado ou mudado de lugar, e a cena é descrita inteira de novo.
    /// </summary>
    public static ContextoAnterior? Contexto(Descricao? anterior, ModoDescricao modo, DateTime agora, TimeSpan validade)
    {
        if (anterior is null || modo != ModoDescricao.AMBIENTE || anterior.Modo != ModoDescricao.AMBIENTE)
            return null;
        var idade = agora - anterior.CriadoEm;
        if (idade > validade)
            return null;
        return new ContextoAnterior(anterior.DescricaoCompleta, anterior.Resumo, anterior.Perigos, Math.Max(0, (long)idade.TotalSeconds));
    }

    public async Task<PaginaResposta<DescricaoResposta>> ListarAsync(long usuarioId, int? page, int? size, CancellationToken ct = default)
    {
        var p = Paginacao.De(page, size);
        var pagina = await descricoes.DoUsuarioAsync(usuarioId, p.Pagina, p.Tamanho, ct);
        return PaginaResposta<DescricaoResposta>.De(pagina, p, d => DescricaoResposta.De(d));
    }

    public async Task<DescricaoResposta> BuscarAsync(long usuarioId, long id, CancellationToken ct = default) =>
        DescricaoResposta.De(await DaPessoaAsync(usuarioId, id, ct));

    public async Task<DescricaoResposta> AvaliarAsync(long usuarioId, long id, bool util, CancellationToken ct = default)
    {
        var descricao = await DaPessoaAsync(usuarioId, id, ct);
        descricao.Avaliar(util);
        await uow.SalvarAsync(ct);
        return DescricaoResposta.De(descricao);
    }

    /// <summary>Relatos criados a partir desta descrição continuam existindo (DESCRICAO_ID vira NULL no banco).</summary>
    public async Task ExcluirAsync(long usuarioId, long id, CancellationToken ct = default)
    {
        descricoes.Remover(await DaPessoaAsync(usuarioId, id, ct));
        await uow.SalvarAsync(ct);
    }

    /// <summary>Pergunta livre por voz sobre a foto: exige a preferência ativa e nada é gravado.</summary>
    public async Task<PerguntaResposta> PerguntarAsync(long usuarioId, long id, string? pergunta, byte[] foto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pergunta) || pergunta.Length > TamanhoPergunta)
            throw RegraNegocioException.Invalida("Faça uma pergunta de até 500 caracteres.");

        var usuario = await usuarios.BuscarComRegioesAsync(usuarioId, ct) ?? throw RegraNegocioException.NaoEncontrado("Usuário não encontrado.");
        if (!usuario.PerguntaLivreAtiva)
            throw RegraNegocioException.Proibido("Ative a pergunta livre por voz nas preferências.");

        await DaPessoaAsync(usuarioId, id, ct);
        var imagem = ValidarFoto(foto);
        return new PerguntaResposta(await descritor.ResponderAsync(imagem, pergunta.Trim(), ct));
    }

    /// <summary>JPEG, PNG ou WebP de até 5 MB, conferido pela assinatura dos bytes.</summary>
    public static ImagemRecebida ValidarFoto(byte[]? foto)
    {
        if (foto is null || foto.Length == 0)
            throw RegraNegocioException.Invalida("Envie a foto no campo imagem.");
        if (foto.Length > AssinaturaImagem.TamanhoMaximoBytes)
            throw new RegraNegocioException(TipoErro.ArquivoGrande, "A foto passa de 5 MB. Envie uma foto menor.");
        var tipo = AssinaturaImagem.Detectar(foto)
                   ?? throw new RegraNegocioException(TipoErro.FormatoNaoSuportado, "Formato de foto não suportado. Use JPEG, PNG ou WebP.");
        return new ImagemRecebida(foto, tipo);
    }

    private async Task<Descricao> DaPessoaAsync(long usuarioId, long id, CancellationToken ct, string mensagem = "Descrição não encontrada.") =>
        await descricoes.BuscarDoUsuarioAsync(id, usuarioId, ct) ?? throw RegraNegocioException.NaoEncontrado(mensagem);

    private DateTime Agora() => relogio.GetUtcNow().UtcDateTime;

    private static string Limitar(string? texto, int max) =>
        texto is null ? "" : texto.Length <= max ? texto : texto[..max];
}

/// <summary>
/// No modo automático a mesma barreira aparece em várias fotos seguidas. Esta memória evita perguntar
/// "Quer avisar a rede de apoio?" a cada foto: a mesma sugestão só volta depois da janela (2 min).
/// No modo manual a pessoa fotografou de propósito, então a sugestão sempre aparece.
/// </summary>
public sealed class MemoriaSugestoes(IOptions<IaOptions> opcoes, TimeProvider relogio)
{
    private readonly ConcurrentDictionary<(long Usuario, TipoRelato Tipo), DateTimeOffset> _ultimas = new();

    public bool DeveSugerir(long usuarioId, TipoRelato tipo, bool automatico)
    {
        var agora = relogio.GetUtcNow();
        var janela = TimeSpan.FromSeconds(opcoes.Value.JanelaSugestaoSegundos);
        var sugerir = true;
        _ultimas.AddOrUpdate((usuarioId, tipo), agora, (_, ultima) =>
        {
            if (automatico && ultima + janela > agora)
            {
                sugerir = false;
                return ultima;
            }
            return agora;
        });
        LimparVencidas(agora - janela);
        return sugerir;
    }

    private void LimparVencidas(DateTimeOffset corte)
    {
        if (_ultimas.Count < 1000)
            return;
        foreach (var (chave, ultima) in _ultimas)
            if (ultima < corte)
                _ultimas.TryRemove(chave, out _);
    }
}
