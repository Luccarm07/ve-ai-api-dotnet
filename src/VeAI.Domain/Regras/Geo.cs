namespace VeAI.Domain.Regras;

/// <summary>Cálculos geográficos simples, sem extensões espaciais do banco.</summary>
public static class Geo
{
    /// <summary>Raio de deduplicação: relato novo a até 30 m de um existente vira apoio.</summary>
    public const double RaioDeduplicacaoMetros = 30;

    /// <summary>Raio médio da Terra, o mesmo da função PL/SQL fn_distancia_metros.</summary>
    public const double RaioTerraMetros = 6_371_000d;

    private const double GrausPorMetro = 1d / 111_320d;

    /// <summary>Distância em metros entre dois pontos pela fórmula de Haversine.</summary>
    public static double DistanciaMetros(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = Radianos(lat2 - lat1);
        var dLon = Radianos(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Radianos(lat1)) * Math.Cos(Radianos(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * RaioTerraMetros * Math.Asin(Math.Min(1d, Math.Sqrt(a)));
    }

    /// <summary>Margem em graus de latitude para um raio em metros (pré-filtro por "caixa" no banco).</summary>
    public static double MargemLatitude(double metros) => metros * GrausPorMetro;

    /// <summary>Margem em graus de longitude: o grau de longitude encolhe com o cosseno da latitude.</summary>
    public static double MargemLongitude(double metros, double latitude) =>
        metros * GrausPorMetro / Math.Max(0.01, Math.Cos(Radianos(latitude)));

    private static double Radianos(double graus) => graus * Math.PI / 180d;
}
