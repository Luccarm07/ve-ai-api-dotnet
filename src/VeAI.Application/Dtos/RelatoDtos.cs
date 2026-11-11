using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using VeAI.Domain.Entities;
using VeAI.Domain.Enums;

namespace VeAI.Application.Dtos;

public sealed record RelatoRequest
{
    [Required(ErrorMessage = "Informe o tipo da barreira.")]
    public TipoRelato? Tipo { get; init; }

    [Required(ErrorMessage = "Informe a gravidade.")]
    public Gravidade? Gravidade { get; init; }

    [Required(ErrorMessage = "Descreva o problema."), StringLength(1000, ErrorMessage = "O texto pode ter no máximo 1000 caracteres.")]
    [DefaultValue("Buraco grande na calçada, em frente ao número 500.")]
    public string? Texto { get; init; }

    [Required(ErrorMessage = "A localização é obrigatória."), Range(-90.0, 90.0, ErrorMessage = "Latitude inválida.")]
    [DefaultValue(-23.56612)]
    public double? Latitude { get; init; }

    [Required(ErrorMessage = "A localização é obrigatória."), Range(-180.0, 180.0, ErrorMessage = "Longitude inválida.")]
    [DefaultValue(-46.68431)]
    public double? Longitude { get; init; }

    [StringLength(200, ErrorMessage = "O endereço pode ter no máximo 200 caracteres.")]
    [DefaultValue("Rua dos Pinheiros, 500")]
    public string? Endereco { get; init; }

    [Description("Descrição da IA que originou o relato (opcional)")]
    public long? DescricaoId { get; init; }

    [Description("Opcional: se ausente, a API usa a região de centro mais próximo")]
    public long? RegiaoId { get; init; }
}

public sealed record RelatoEdicaoRequest
{
    [Required(ErrorMessage = "Informe o tipo da barreira.")]
    public TipoRelato? Tipo { get; init; }

    [Required(ErrorMessage = "Informe a gravidade.")]
    public Gravidade? Gravidade { get; init; }

    [Required(ErrorMessage = "Descreva o problema."), StringLength(1000, ErrorMessage = "O texto pode ter no máximo 1000 caracteres.")]
    public string? Texto { get; init; }

    [StringLength(200, ErrorMessage = "O endereço pode ter no máximo 200 caracteres.")]
    public string? Endereco { get; init; }
}

public sealed record StatusRequest
{
    [Required(ErrorMessage = "Informe o novo status.")]
    public StatusRelato? Status { get; init; }

    [StringLength(40, ErrorMessage = "O protocolo pode ter no máximo 40 caracteres.")]
    [Description("Obrigatório para ENCAMINHADO"), DefaultValue("SP156-2026-0012345")]
    public string? ProtocoloOrgao { get; init; }

    [StringLength(500, ErrorMessage = "O comentário pode ter no máximo 500 caracteres.")]
    public string? Comentario { get; init; }

    [StringLength(300, ErrorMessage = "O motivo pode ter no máximo 300 caracteres.")]
    [Description("Obrigatório para DESCARTADO")]
    public string? MotivoDescarte { get; init; }
}

public sealed record RegiaoResumo(long Id, string Nome);

public sealed record RelatoResposta(
    long Id, long CidadaoId, long? DescricaoId, RegiaoResumo Regiao, TipoRelato Tipo, Gravidade Gravidade, string Texto,
    double Latitude, double Longitude, string? Endereco, StatusRelato Status, string? ProtocoloOrgao, string? MotivoDescarte,
    int TotalApoios, int Prioridade, string? NormaReferencia, DateTime CriadoEm, DateTime AtualizadoEm)
{
    public static RelatoResposta De(Relato r) => new(
        r.Id, r.CidadaoId, r.DescricaoId, new RegiaoResumo(r.RegiaoId, r.Regiao?.Nome ?? ""), r.Tipo, r.Gravidade, r.Texto,
        r.Latitude, r.Longitude, r.Endereco, r.Status, r.ProtocoloOrgao, r.MotivoDescarte, r.TotalApoios, r.Prioridade,
        r.NormaReferencia, r.CriadoEm, r.AtualizadoEm);
}

/// <summary>Mesmos resultados da procedure prc_registrar_relato_ou_apoio do Oracle.</summary>
public enum ResultadoRegistro
{
    CRIADO,
    APOIO_REGISTRADO,
    JA_REGISTRADO
}

/// <summary>201 quando cria o relato; 200 quando já existe um igual a até 30 m (apoio ou já registrado).</summary>
public sealed record RegistroRelatoResposta(ResultadoRegistro Resultado, bool ApoioRegistrado, RelatoResposta Relato)
{
    public int StatusHttp => Resultado == ResultadoRegistro.CRIADO ? 201 : 200;
}

public sealed record AtualizacaoResposta(long Id, StatusRelato StatusAnterior, StatusRelato StatusNovo, string? Comentario,
                                         long GestorId, string GestorNome, DateTime CriadoEm)
{
    public static AtualizacaoResposta De(AtualizacaoRelato a) =>
        new(a.Id, a.StatusAnterior, a.StatusNovo, a.Comentario, a.GestorId, a.Gestor?.Nome ?? "", a.CriadoEm);
}

/// <summary>Página no formato do contrato: conteudo, pagina, tamanho, totalElementos, totalPaginas.</summary>
public sealed record PaginaResposta<T>(IReadOnlyList<T> Conteudo, int Pagina, int Tamanho, long TotalElementos, int TotalPaginas)
{
    public static PaginaResposta<T> De<TOrigem>(Abstracoes.Pagina<TOrigem> pagina, Paginacao p, Func<TOrigem, T> mapear) =>
        new(pagina.Itens.Select(mapear).ToList(), p.Pagina, p.Tamanho, pagina.Total,
            (int)Math.Ceiling(pagina.Total / (double)p.Tamanho));
}

/// <summary>Normaliza ?page e ?size do contrato: página a partir de 0, tamanho padrão 10 e máximo 50.</summary>
public readonly record struct Paginacao(int Pagina, int Tamanho)
{
    public const int TamanhoPadrao = 10;
    public const int TamanhoMaximo = 50;

    public static Paginacao De(int? page, int? size) => new(
        page is null or < 0 ? 0 : page.Value,
        size is null or < 1 ? TamanhoPadrao : Math.Min(size.Value, TamanhoMaximo));
}
