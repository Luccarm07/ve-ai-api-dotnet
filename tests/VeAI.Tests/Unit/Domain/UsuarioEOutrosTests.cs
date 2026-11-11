using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Excecoes;

namespace VeAI.Tests.Unit.Domain;

public class UsuarioEOutrosTests
{
    private static readonly DateTime Agora = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact(DisplayName = "cidadão novo: e-mail normalizado e preferências padrão")]
    public void NovoCidadao()
    {
        var u = Usuario.NovoCidadao(" Ana ", " Ana.Souza@VEAI.dev ", "hash", Agora);

        Assert.Equal("Ana", u.Nome);
        Assert.Equal("ana.souza@veai.dev", u.Email);
        Assert.Equal(Papel.CIDADAO, u.Papel);
        Assert.Equal(1.0, u.VelocidadeVoz);
        Assert.False(u.PerguntaLivreAtiva);
    }

    [Theory(DisplayName = "velocidade da voz fora de 0,5–2 é recusada")]
    [InlineData(0.4)]
    [InlineData(2.1)]
    public void VelocidadeInvalida(double velocidade)
    {
        var u = Usuario.NovoCidadao("Ana", "ana@veai.dev", "hash", Agora);

        Assert.Equal(TipoErro.RequisicaoInvalida,
            Assert.Throws<RegraNegocioException>(() => u.AtualizarPreferencias(velocidade, true)).Tipo);
    }

    [Fact(DisplayName = "preferências válidas são gravadas")]
    public void PreferenciasValidas()
    {
        var u = Usuario.NovoCidadao("Ana", "ana@veai.dev", "hash", Agora);

        u.AtualizarPreferencias(1.25, true);

        Assert.Equal(1.25, u.VelocidadeVoz);
        Assert.True(u.PerguntaLivreAtiva);
    }

    [Fact(DisplayName = "gestor só atende as próprias regiões; cidadão não atende nenhuma")]
    public void GestorAtendeRegioes()
    {
        var cidadao = Usuario.NovoCidadao("Ana", "ana@veai.dev", "hash", Agora);
        var gestor = Usuario.NovoGestor("Carlos", "carlos@veai.dev", "hash", [], Agora);

        Assert.Equal(Papel.GESTOR, gestor.Papel);
        Assert.False(gestor.Atende(1));
        Assert.False(cidadao.Atende(0));
    }

    [Fact(DisplayName = "notificação longa é cortada em 300 caracteres e começa não lida")]
    public void Notificacao()
    {
        var n = new Notificacao(1, 1, new string('a', 400), Agora);

        Assert.Equal(300, n.Mensagem.Length);
        Assert.False(n.Lida);
        n.MarcarComoLida();
        Assert.True(n.Lida);
    }

    [Fact(DisplayName = "idempotência: mesma chave vale 24 h e confere o hash do corpo")]
    public void Idempotencia()
    {
        var r = new RegistroIdempotencia(1, "chave-1", "abc", 201, "{}", Agora);

        Assert.True(r.MesmaRequisicao("abc"));
        Assert.False(r.MesmaRequisicao("xyz"));
        Assert.False(r.Expirado(Agora.AddHours(23)));
        Assert.True(r.Expirado(Agora.AddHours(25)));
    }

    [Fact(DisplayName = "histórico de status guarda de/para e apara o comentário")]
    public void Atualizacao()
    {
        var a = new AtualizacaoRelato(1, 2, StatusRelato.ABERTO, StatusRelato.EM_TRIAGEM, "  ", Agora);

        Assert.Null(a.Comentario);
        Assert.Equal(StatusRelato.EM_TRIAGEM, a.StatusNovo);
    }
}
