using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using VeAI.Domain.Entities;
using VeAI.Domain.Enums;

namespace VeAI.Application.Dtos;

// Mesmos nomes de campo, regras e mensagens da API Java (o app mobile consome as duas).

public sealed record RegistroRequest
{
    [Required(ErrorMessage = "Informe seu nome."), StringLength(120, ErrorMessage = "O nome pode ter no máximo 120 caracteres.")]
    [Description("Nome completo")]
    public string? Nome { get; init; }

    [Required(ErrorMessage = "Informe seu e-mail."), EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(160, ErrorMessage = "O e-mail pode ter no máximo 160 caracteres.")]
    public string? Email { get; init; }

    [Required(ErrorMessage = "Informe uma senha.")]
    [StringLength(72, MinimumLength = 8, ErrorMessage = "A senha precisa ter entre 8 e 72 caracteres.")]
    public string? Senha { get; init; }
}

public sealed record LoginRequest
{
    [Required(ErrorMessage = "Informe seu e-mail."), EmailAddress(ErrorMessage = "E-mail inválido.")]
    [DefaultValue("ana.souza@veai.dev")]
    public string? Email { get; init; }

    [Required(ErrorMessage = "Informe sua senha.")]
    [DefaultValue("Veai@2026")]
    public string? Senha { get; init; }
}

public sealed record PreferenciasRequest
{
    [Required(ErrorMessage = "Informe a velocidade da voz.")]
    [Range(0.5, 2.0, ErrorMessage = "A velocidade da voz vai de 0,5 a 2,0.")]
    [DefaultValue(1.25)]
    public double? VelocidadeVoz { get; init; }

    [Required(ErrorMessage = "Informe se a pergunta livre por voz fica ativa.")]
    public bool? PerguntaLivreAtiva { get; init; }
}

public sealed record RegiaoResposta(long Id, string Nome, string Cidade, string Uf, double? Latitude, double? Longitude)
{
    public static RegiaoResposta De(Regiao r) => new(r.Id, r.Nome, r.Cidade, r.Uf.Trim(), r.Latitude, r.Longitude);
}

/// <summary>Dados públicos do usuário. "regioes" só vem preenchido para gestores.</summary>
public sealed record UsuarioResposta(long Id, string Nome, string Email, Papel Papel, double VelocidadeVoz,
                                     bool PerguntaLivreAtiva, DateTime CriadoEm, IReadOnlyList<RegiaoResposta> Regioes)
{
    public static UsuarioResposta De(Usuario u) => new(
        u.Id, u.Nome, u.Email, u.Papel, u.VelocidadeVoz, u.PerguntaLivreAtiva, u.CriadoEm,
        u.Papel == Papel.GESTOR ? u.Regioes.Select(RegiaoResposta.De).OrderBy(r => r.Nome).ToList() : []);
}

public sealed record TokenResposta(string Token, string Tipo, DateTime ExpiraEm, Papel Papel, UsuarioResposta Usuario);
