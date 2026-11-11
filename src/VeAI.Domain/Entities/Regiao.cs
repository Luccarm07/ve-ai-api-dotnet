using VeAI.Domain.Regras;

namespace VeAI.Domain.Entities;

/// <summary>
/// Bairro/subprefeitura atendido pela rede de apoio. Latitude/longitude = centro aproximado,
/// usado para escolher a região do relato pelo GPS (colunas da V3, opcionais).
/// </summary>
public class Regiao
{
    public long Id { get; private set; }
    public string Nome { get; private set; } = null!;
    public string Cidade { get; private set; } = null!;
    public string Uf { get; private set; } = null!;
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    private Regiao() { }

    public Regiao(string nome, string cidade, string uf, double? latitude, double? longitude)
    {
        Nome = nome;
        Cidade = cidade;
        Uf = uf;
        Latitude = latitude;
        Longitude = longitude;
    }

    public bool TemCentro => Latitude.HasValue && Longitude.HasValue;

    /// <summary>Região cujo centro está mais perto do ponto informado (null se nenhuma tem centro).</summary>
    public static Regiao? MaisProxima(IEnumerable<Regiao> regioes, double latitude, double longitude) =>
        regioes.Where(r => r.TemCentro)
               .OrderBy(r => Geo.DistanciaMetros(latitude, longitude, r.Latitude!.Value, r.Longitude!.Value))
               .FirstOrDefault();
}
