namespace VeAI.Domain.Regras;

/// <summary>
/// Confere o formato da foto pelos primeiros bytes (assinatura), e não só pelo Content-Type do cliente.
/// Aceita JPEG, PNG e WebP de até 5 MB.
/// </summary>
public static class AssinaturaImagem
{
    public const long TamanhoMaximoBytes = 5L * 1024 * 1024;

    /// <summary>Devolve o MIME type detectado ou null se não for JPEG, PNG ou WebP.</summary>
    public static string? Detectar(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G'
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return "image/png";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
            && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
            return "image/webp";
        return null;
    }
}
