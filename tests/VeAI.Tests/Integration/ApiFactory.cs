using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;
using VeAI.Application.Abstracoes;
using VeAI.Domain.Entities;
using VeAI.Domain.Eventos;
using VeAI.Infrastructure.Persistence;

namespace VeAI.Tests.Integration;

/// <summary>
/// Sobe a API inteira em memória (WebApplicationFactory) trocando o Oracle por SQLite em memória,
/// com o mesmo modelo do EF Core e um seed pequeno. Assim os testes rodam em qualquer máquina, sem Docker.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string SegredoJwt = "segredo-so-dos-testes-automatizados-com-mais-de-32-caracteres";

    private readonly SqliteConnection _conexao = new("DataSource=:memory:");

    /// <summary>Eventos publicados (no lugar do RabbitMQ), para os testes conferirem o que foi avisado.</summary>
    public EventosCapturados Eventos { get; } = new();

    /// <summary>Cliente já autenticado com o usuário informado (senha do seed: Veai@2026).</summary>
    public async Task<HttpClient> ClienteLogadoAsync(string email)
    {
        var cliente = CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/auth/login", new { email, senha = DadosTeste.Senha });
        resposta.EnsureSuccessStatusCode();
        var token = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    public ApiFactory()
    {
        _conexao.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Oracle", "nao-usado-nos-testes");
        builder.UseSetting("Jwt:Segredo", SegredoJwt);
        builder.UseSetting("Manutencao:Ativa", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<VeAiDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<VeAiDbContext>>();
            services.AddDbContext<VeAiDbContext>(o => o.UseSqlite(_conexao));
            services.RemoveAll<IPublicadorEventos>();
            services.AddSingleton<IPublicadorEventos>(Eventos);

            using var escopo = services.BuildServiceProvider().CreateScope();
            var db = escopo.ServiceProvider.GetRequiredService<VeAiDbContext>();
            db.Database.EnsureCreated();
            DadosTeste.Popular(db);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _conexao.Dispose();
    }
}

/// <summary>Seed mínimo dos testes, com os mesmos nomes, e-mails e senha (Veai@2026) do seed do Oracle.</summary>
public static class DadosTeste
{
    public const string Senha = "Veai@2026";
    /// <summary>Mesmo hash BCrypt do seed Oracle (database/oracle/04_seed.sql).</summary>
    public const string SenhaHash = "$2a$10$xCHtq1/BEvjc2Bwk9iQ37O84OOuuVQPqIF/pYmbI0hxqFP3jXuOBi";

    public const string EmailAna = "ana.souza@veai.dev";
    public const string EmailBruno = "bruno.lima@veai.dev";
    public const string EmailMarina = "marina.alves@ong-veai.dev";   // gestora de Pinheiros e Sé
    public const string EmailRafael = "rafael.costa@cras-veai.dev";  // gestor de Vila Mariana

    public static void Popular(VeAiDbContext db)
    {
        if (db.Usuarios.Any())
            return;

        var agora = DateTime.UtcNow;
        var pinheiros = new Regiao("Pinheiros", "São Paulo", "SP", -23.5662, -46.6890);
        var se = new Regiao("Sé", "São Paulo", "SP", -23.5503, -46.6339);
        var vilaMariana = new Regiao("Vila Mariana", "São Paulo", "SP", -23.5889, -46.6355);
        db.Regioes.AddRange(pinheiros, se, vilaMariana);

        db.Usuarios.AddRange(
            Usuario.NovoCidadao("Ana Souza", EmailAna, SenhaHash, agora.AddDays(-90)),
            Usuario.NovoCidadao("Bruno Lima", EmailBruno, SenhaHash, agora.AddDays(-85)),
            Usuario.NovoGestor("Marina Alves", EmailMarina, SenhaHash, [pinheiros, se], agora.AddDays(-100)),
            Usuario.NovoGestor("Rafael Costa", EmailRafael, SenhaHash, [vilaMariana], agora.AddDays(-100)));
        db.SaveChanges();
    }
}

/// <summary>Publicador de eventos dos testes: guarda em memória o que seria enviado ao RabbitMQ.</summary>
public sealed class EventosCapturados : IPublicadorEventos
{
    private readonly ConcurrentQueue<EventoRelato> _eventos = new();

    public IReadOnlyList<EventoRelato> Todos => _eventos.ToList();

    public Task PublicarAsync(EventoRelato evento, CancellationToken ct = default)
    {
        _eventos.Enqueue(evento);
        return Task.CompletedTask;
    }
}
