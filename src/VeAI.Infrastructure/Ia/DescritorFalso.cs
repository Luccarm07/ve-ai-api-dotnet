using VeAI.Application.Abstracoes;
using VeAI.Domain.Enums;
using VeAI.Domain.Modelos;

namespace VeAI.Infrastructure.Ia;

/// <summary>
/// Descritor sem IA, para desenvolvimento, testes e demonstração sem custo (padrão: Ia__Provedor=falso).
/// A mesma foto sempre gera a mesma resposta (o cenário sai do conteúdo dos bytes, com o mesmo cálculo da API Java),
/// e há um cenário para cada caminho do app: barreira com confiança alta, só perigo momentâneo, buraco e foto escura.
/// A mesma cena repetida no modo automático volta com novidade = false e uma confirmação curta.
/// </summary>
public sealed class DescritorFalso : IDescritorImagem
{
    private static readonly DescricaoIA[] Cenarios =
    [
        new([new Perigo("Poste no meio da calçada, deixando pouco espaço para passar.", "frente", "cerca de 3 metros")],
            "Poste no meio da calçada, a cerca de 3 metros. Calçada estreita.",
            "Bem à sua frente, a cerca de 3 metros, há um poste no meio da calçada, deixando pouco espaço para passar. " +
            "Calçada estreita, de concreto. Ao seu lado direito, um muro baixo acompanha a calçada.",
            true, ["poste", "muro", "lixeira"], null,
            new Barreira(true, TipoRelato.OBSTRUCAO_PERMANENTE, Gravidade.MEDIA, 0.86, "Poste fixo ocupando a faixa livre da calçada.")),
        new([new Perigo("Bicicleta vindo em sua direção pela calçada.", "frente-esquerda", "cerca de 5 metros")],
            "Calçada larga e plana.",
            "À frente, levemente à esquerda, a cerca de 5 metros, uma bicicleta vem em sua direção. " +
            "Calçada larga, de piso regular. À frente, mais para a direita, há uma árvore.",
            true, ["bicicleta", "árvore"], null, Barreira.Nenhuma()),
        new([new Perigo("Buraco largo no piso da calçada.", "frente", "cerca de 2 metros")],
            "Buraco na calçada, a cerca de 2 metros. Dá para desviar pela direita.",
            "Bem à sua frente, a cerca de 2 metros, há um buraco largo no piso. " +
            "Dá para desviar pela direita, junto ao meio-fio. Calçada de concreto.",
            true, ["buraco", "meio-fio"], null,
            new Barreira(true, TipoRelato.CALCADA_DANIFICADA, Gravidade.ALTA, 0.93, "Buraco profundo na faixa de circulação.")),
        new([],
            "A foto está escura. Aponte o celular para a frente, com mais luz.",
            "A foto está escura demais para descrever o ambiente com segurança. " +
            "Tente de novo com mais luz. Não posso confirmar se o caminho está livre.",
            true, [], null,
            new Barreira(true, TipoRelato.OUTRO, Gravidade.BAIXA, 0.35, "Imagem escura: baixa confiança."))
    ];

    private static readonly DescricaoIA Texto = new(
        [], "Placa: Farmácia aberta 24 horas.", "Farmácia São Paulo. Aberta 24 horas. Entrada pela lateral.",
        true, ["placa"], "Farmácia São Paulo. Aberta 24 horas. Entrada pela lateral.", Barreira.Nenhuma());

    public string Provedor => "falso";

    public Task<DescricaoIA> DescreverAsync(ImagemRecebida imagem, ModoDescricao modo, ContextoAnterior? anterior,
                                            CancellationToken ct = default)
    {
        if (modo == ModoDescricao.TEXTO)
            return Task.FromResult(Texto);

        var cena = Cenarios[CenarioDe(imagem.Bytes)];
        // Simula a comparação do modelo: mesma cena da foto anterior = sem novidade, confirmação curta.
        if (anterior is not null && cena.Resumo == anterior.Resumo)
            cena = cena with { DescricaoCompleta = Confirmacao(cena.Objetos!), Novidade = false };
        return Task.FromResult(cena);
    }

    public Task<string> ResponderAsync(ImagemRecebida imagem, string pergunta, CancellationToken ct = default) =>
        Task.FromResult("Não vejo degraus à sua frente. O poste fica à frente, levemente à direita, a cerca de 3 metros.");

    /// <summary>"Poste, muro e lixeira continuam no mesmo lugar, à sua frente."</summary>
    public static string Confirmacao(IReadOnlyList<string> objetos)
    {
        if (objetos.Count == 0)
            return "Nada mudou à sua frente.";
        var lista = objetos.Count == 1 ? objetos[0] : string.Join(", ", objetos.Take(objetos.Count - 1)) + " e " + objetos[^1];
        var verbo = objetos.Count == 1 ? " continua" : " continuam";
        return char.ToUpperInvariant(lista[0]) + lista[1..] + verbo + " no mesmo lugar, à sua frente.";
    }

    /// <summary>Índice do cenário de uma imagem: Arrays.hashCode(byte[]) do Java, para as duas APIs concordarem.</summary>
    public static int CenarioDe(byte[] bytes)
    {
        var hash = 1;
        foreach (var b in bytes)
            hash = unchecked(31 * hash + (sbyte)b);
        return (int)(((long)hash % Cenarios.Length + Cenarios.Length) % Cenarios.Length);
    }
}
