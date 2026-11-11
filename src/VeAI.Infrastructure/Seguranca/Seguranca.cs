using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VeAI.Application.Abstracoes;
using VeAI.Domain.Entities;

namespace VeAI.Infrastructure.Seguranca;

/// <summary>Configuração do JWT (seção "Jwt"). O segredo vem só do .env / variável Jwt__Segredo.</summary>
public sealed class JwtOptions
{
    public const string Secao = "Jwt";
    public const string ClaimPapel = "papel";

    /// <summary>Chave HS256 com no mínimo 32 caracteres.</summary>
    public string Segredo { get; set; } = "";
    public string Emissor { get; set; } = "ve-ai-api";
    public int ValidadeHoras { get; set; } = 8;

    public SymmetricSecurityKey Chave() => new(Encoding.UTF8.GetBytes(Segredo));
}

/// <summary>Emite o JWT (HS256): sub = id do usuário, claim "papel" = CIDADAO | GESTOR (igual à API Java).</summary>
internal sealed class GeradorTokenJwt(IOptions<JwtOptions> opcoes, TimeProvider relogio) : IGeradorToken
{
    private readonly JsonWebTokenHandler _handler = new();

    public TokenEmitido Emitir(Usuario usuario)
    {
        var config = opcoes.Value;
        var agora = relogio.GetUtcNow().UtcDateTime;
        var expiraEm = agora.AddHours(config.ValidadeHoras);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = config.Emissor,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = expiraEm,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtOptions.ClaimPapel, usuario.Papel.ToString()),
                new Claim("nome", usuario.Nome)
            ]),
            SigningCredentials = new SigningCredentials(config.Chave(), SecurityAlgorithms.HmacSha256)
        });

        return new TokenEmitido(token, DateTime.SpecifyKind(expiraEm, DateTimeKind.Utc));
    }
}

/// <summary>BCrypt (fator 10), compatível com os hashes "$2a$" do seed Oracle e da API Java.</summary>
internal sealed class SenhaHasherBCrypt : ISenhaHasher
{
    public string Gerar(string senha) => BCrypt.Net.BCrypt.HashPassword(senha, workFactor: 10);

    public bool Confere(string senha, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(senha, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false; // hash ilustrativo/inválido no banco: trata como senha incorreta
        }
    }
}
