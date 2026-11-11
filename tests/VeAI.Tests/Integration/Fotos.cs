using System.Net.Http.Headers;
using VeAI.Infrastructure.Ia;

namespace VeAI.Tests.Integration;

/// <summary>Fotos de teste: JPEGs mínimos escolhidos para cair em cada cenário do descritor falso.</summary>
public static class Fotos
{
    public const int Poste = 0, Bicicleta = 1, Buraco = 2, Escura = 3;

    public static byte[] Jpeg(int cenario)
    {
        for (byte extra = 0; ; extra++)
        {
            byte[] bytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, extra];
            if (DescritorFalso.CenarioDe(bytes) == cenario)
                return bytes;
        }
    }

    public static MultipartFormDataContent Formulario(byte[] foto, string tipo = "image/jpeg", params (string Campo, string Valor)[] campos)
    {
        var form = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(foto);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        form.Add(arquivo, "imagem", "foto.jpg");
        foreach (var (campo, valor) in campos)
            form.Add(new StringContent(valor), campo);
        return form;
    }
}
