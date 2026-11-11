using VeAI.Domain.Enums;
using VeAI.Domain.Excecoes;
using VeAI.Domain.Regras;

namespace VeAI.Domain.Entities;

/// <summary>Barreira de acessibilidade relatada por um cidadão. Tabela RELATO.</summary>
public class Relato
{
    public long Id { get; private set; }
    public long CidadaoId { get; private set; }
    public Usuario? Cidadao { get; private set; }
    public long? DescricaoId { get; private set; }
    public Descricao? Descricao { get; private set; }
    public long RegiaoId { get; private set; }
    public Regiao? Regiao { get; private set; }
    public TipoRelato Tipo { get; private set; }
    public Gravidade Gravidade { get; private set; }
    public string Texto { get; private set; } = null!;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public string? Endereco { get; private set; }
    public StatusRelato Status { get; private set; }
    public string? ProtocoloOrgao { get; private set; }
    public string? MotivoDescarte { get; private set; }
    public int TotalApoios { get; private set; }
    public int Prioridade { get; private set; }
    public string? NormaReferencia { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime AtualizadoEm { get; private set; }

    private Relato() { }

    public static Relato Novo(long cidadaoId, long? descricaoId, Regiao regiao, TipoRelato tipo, Gravidade gravidade,
                              string texto, double latitude, double longitude, string? endereco, DateTime agora)
    {
        var relato = new Relato
        {
            CidadaoId = cidadaoId,
            DescricaoId = descricaoId,
            Regiao = regiao,
            RegiaoId = regiao.Id,
            Tipo = tipo,
            Gravidade = gravidade,
            Texto = texto.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            Endereco = string.IsNullOrWhiteSpace(endereco) ? null : endereco.Trim(),
            Status = StatusRelato.ABERTO,
            CriadoEm = agora,
            AtualizadoEm = agora
        };
        relato.RecalcularPrioridade(agora);
        return relato;
    }

    /// <summary>Distância em metros até um ponto (usada na regra dos 30 m).</summary>
    public double DistanciaAte(double latitude, double longitude) =>
        Geo.DistanciaMetros(Latitude, Longitude, latitude, longitude);

    /// <summary>
    /// Regra dos 30 m: um relato novo do mesmo tipo, a até 30 m de um relato ainda não finalizado,
    /// vira apoio a este em vez de criar outro.
    /// </summary>
    public bool AbsorveNovoRelato(TipoRelato tipo, double latitude, double longitude) =>
        Tipo == tipo && !Status.EhFinal() && DistanciaAte(latitude, longitude) <= Geo.RaioDeduplicacaoMetros;

    public void RegistrarApoio(DateTime agora)
    {
        TotalApoios++;
        AtualizadoEm = agora;
        RecalcularPrioridade(agora);
    }

    /// <summary>Edição pelo autor: só enquanto ABERTO e sem mudar coordenadas (quebraria a deduplicação).</summary>
    public void Editar(TipoRelato tipo, Gravidade gravidade, string texto, string? endereco, DateTime agora)
    {
        if (Status != StatusRelato.ABERTO)
            throw RegraNegocioException.Regra("Só é possível editar um relato que ainda está aberto.");

        Tipo = tipo;
        Gravidade = gravidade;
        Texto = texto.Trim();
        Endereco = string.IsNullOrWhiteSpace(endereco) ? null : endereco.Trim();
        AtualizadoEm = agora;
        RecalcularPrioridade(agora);
    }

    /// <summary>Aplica a máquina de estados e as exigências de protocolo e motivo. Devolve o status anterior.</summary>
    public StatusRelato AlterarStatus(StatusRelato novo, string? protocolo, string? motivo, DateTime agora)
    {
        if (!Status.PodeIrPara(novo))
            throw RegraNegocioException.Regra($"Não é possível mudar o relato de {Status} para {novo}.");
        if (novo == StatusRelato.ENCAMINHADO && string.IsNullOrWhiteSpace(protocolo))
            throw RegraNegocioException.Regra("Informe o protocolo do órgão para encaminhar o relato.");
        if (novo == StatusRelato.DESCARTADO && string.IsNullOrWhiteSpace(motivo))
            throw RegraNegocioException.Regra("Informe o motivo do descarte.");

        var anterior = Status;
        Status = novo;
        if (novo == StatusRelato.ENCAMINHADO)
            ProtocoloOrgao = protocolo!.Trim();
        if (novo == StatusRelato.DESCARTADO)
            MotivoDescarte = motivo!.Trim();
        AtualizadoEm = agora;
        RecalcularPrioridade(agora);
        return anterior;
    }

    /// <summary>
    /// Exclusão pelo autor: só enquanto ABERTO (422) e sem apoios (409), porque apagar o relato
    /// apagaria a confirmação de outras pessoas.
    /// </summary>
    public void GarantirQuePodeExcluir()
    {
        if (Status != StatusRelato.ABERTO)
            throw RegraNegocioException.Regra("Só é possível excluir um relato que ainda está aberto.");
        if (TotalApoios > 0)
            throw RegraNegocioException.Conflito("Outras pessoas já confirmaram este relato. Ele não pode ser excluído.");
    }

    public void RecalcularPrioridade(DateTime agora) =>
        Prioridade = CalculadoraPrioridade.Calcular(Gravidade, TotalApoios, Status, CriadoEm, agora);

    public void DefinirNormaReferencia(string? norma) => NormaReferencia = norma;

    public bool EhDoAutor(long usuarioId) => CidadaoId == usuarioId;
}
