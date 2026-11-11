using VeAI.Domain.Entities;
using VeAI.Domain.Regras;

namespace VeAI.Tests.Unit.Domain;

public class GeoTests
{
    [Fact(DisplayName = "Haversine: mesmo ponto = 0 m; pontos do seed a ~10 m; Sé–Pinheiros ~5,9 km")]
    public void Distancias()
    {
        Assert.Equal(0, Geo.DistanciaMetros(-23.5661, -46.6843, -23.5661, -46.6843));
        Assert.InRange(Geo.DistanciaMetros(-23.56612, -46.68431, -23.56620, -46.68436), 8.0, 12.0);
        Assert.InRange(Geo.DistanciaMetros(-23.5503, -46.6339, -23.5662, -46.6890), 5_600, 6_200);
    }

    [Fact(DisplayName = "a caixa do pré-filtro sempre contém o círculo de 30 m")]
    public void CaixaContemOCirculo()
    {
        const double lat = -23.55;
        var dLat = Geo.MargemLatitude(30);
        var dLon = Geo.MargemLongitude(30, lat);

        Assert.True(Geo.DistanciaMetros(lat, -46.63, lat + dLat, -46.63) >= 29.9);
        Assert.True(Geo.DistanciaMetros(lat, -46.63, lat, -46.63 + dLon) >= 29.9);
    }

    [Fact(DisplayName = "região do relato = centro mais próximo")]
    public void RegiaoMaisProxima()
    {
        var pinheiros = new Regiao("Pinheiros", "São Paulo", "SP", -23.5662, -46.6890);
        var se = new Regiao("Sé", "São Paulo", "SP", -23.5503, -46.6339);
        var vilaMariana = new Regiao("Vila Mariana", "São Paulo", "SP", -23.5889, -46.6355);
        var semCentro = new Regiao("Sem centro", "São Paulo", "SP", null, null);
        Regiao[] todas = [semCentro, pinheiros, se, vilaMariana];

        Assert.Same(pinheiros, Regiao.MaisProxima(todas, -23.56612, -46.68431)); // Rua dos Pinheiros
        Assert.Same(se, Regiao.MaisProxima(todas, -23.55052, -46.63331));        // Praça da Sé
        Assert.Same(vilaMariana, Regiao.MaisProxima(todas, -23.5889, -46.6342)); // Domingos de Morais
        Assert.Null(Regiao.MaisProxima([], 0, 0));
        Assert.Null(Regiao.MaisProxima([semCentro], 0, 0));
    }
}
