using VeAI.Domain.Enums;

namespace VeAI.Domain.Entities;

/// <summary>Cidadão com deficiência visual ou gestor da rede de apoio (ONG/CRAS). Tabela USUARIO.</summary>
public class Usuario
{
    public const double VelocidadeVozMinima = 0.5;
    public const double VelocidadeVozMaxima = 2.0;

    public long Id { get; private set; }
    public string Nome { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string SenhaHash { get; private set; } = null!;
    public Papel Papel { get; private set; }
    public double VelocidadeVoz { get; private set; } = 1.0;
    public bool PerguntaLivreAtiva { get; private set; }
    public DateTime CriadoEm { get; private set; }

    /// <summary>Tabela GESTOR_REGIAO: regiões atendidas por um gestor (vazia para cidadãos).</summary>
    public List<Regiao> Regioes { get; private set; } = [];

    private Usuario() { }

    public static Usuario NovoCidadao(string nome, string email, string senhaHash, DateTime agora) =>
        new() { Nome = nome.Trim(), Email = NormalizarEmail(email), SenhaHash = senhaHash, Papel = Papel.CIDADAO, CriadoEm = agora };

    public static Usuario NovoGestor(string nome, string email, string senhaHash, IEnumerable<Regiao> regioes, DateTime agora) =>
        new()
        {
            Nome = nome.Trim(), Email = NormalizarEmail(email), SenhaHash = senhaHash, Papel = Papel.GESTOR,
            CriadoEm = agora, Regioes = regioes.ToList()
        };

    public static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();

    public void AtualizarPreferencias(double velocidadeVoz, bool perguntaLivreAtiva)
    {
        if (velocidadeVoz is < VelocidadeVozMinima or > VelocidadeVozMaxima)
            throw Excecoes.RegraNegocioException.Invalida("A velocidade da voz vai de 0,5 a 2,0.");
        VelocidadeVoz = Math.Round(velocidadeVoz, 2);
        PerguntaLivreAtiva = perguntaLivreAtiva;
    }

    /// <summary>O gestor só vê e altera relatos das regiões que atende.</summary>
    public bool Atende(long regiaoId) => Papel == Papel.GESTOR && Regioes.Any(r => r.Id == regiaoId);
}
