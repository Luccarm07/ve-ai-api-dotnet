using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Modelos;
using VeAI.Infrastructure.Persistence;

namespace VeAI.Tests.Integration;

/// <summary>Grava e relê todas as entidades pelo mapeamento do EF Core (SQLite em memória).</summary>
public class PersistenciaTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "grava e relê o grafo completo: descrição, relato, apoio, histórico, notificação, idempotência")]
    public async Task GrafoCompleto()
    {
        var agora = DateTime.UtcNow;
        long relatoId;

        using (var escopo = factory.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<VeAiDbContext>();
            var ana = await db.Usuarios.SingleAsync(u => u.Email == DadosTeste.EmailAna);
            var bruno = await db.Usuarios.SingleAsync(u => u.Email == DadosTeste.EmailBruno);
            var marina = await db.Usuarios.SingleAsync(u => u.Email == DadosTeste.EmailMarina);
            var pinheiros = await db.Regioes.SingleAsync(r => r.Nome == "Pinheiros");

            var descricao = new Descricao(ana.Id, ModoDescricao.AMBIENTE, "Calçada com buraco à frente", "Detalhes",
                [new Perigo("buraco", "à frente", "2 metros")], ["poste", "lixeira"], true, TipoRelato.CALCADA_DANIFICADA, 850, agora);
            db.Descricoes.Add(descricao);
            await db.SaveChangesAsync();

            var relato = Relato.Novo(ana.Id, descricao.Id, pinheiros, TipoRelato.CALCADA_DANIFICADA, Gravidade.ALTA,
                "Buraco grande", -23.56612, -46.68431, "Rua dos Pinheiros, 100", agora);
            db.Relatos.Add(relato);
            await db.SaveChangesAsync();
            relatoId = relato.Id;

            db.Apoios.Add(new ApoioRelato(relato.Id, bruno.Id, agora));
            relato.RegistrarApoio(agora);
            var anterior = relato.AlterarStatus(StatusRelato.EM_TRIAGEM, null, null, agora);
            db.Atualizacoes.Add(new AtualizacaoRelato(relato.Id, marina.Id, anterior, relato.Status, "Vistoria marcada", agora));
            db.Notificacoes.Add(new Notificacao(ana.Id, relato.Id, "Seu relato está em triagem.", agora));
            db.Idempotencias.Add(new RegistroIdempotencia(ana.Id, "chave-1", new string('a', 64), 201, "{\"id\":1}", agora));
            await db.SaveChangesAsync();
        }

        using (var escopo = factory.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<VeAiDbContext>();
            var relato = await db.Relatos.Include(r => r.Descricao).Include(r => r.Regiao).SingleAsync(r => r.Id == relatoId);

            Assert.Equal(StatusRelato.EM_TRIAGEM, relato.Status);
            Assert.Equal(1, relato.TotalApoios);
            Assert.Equal(10, relato.Prioridade); // ALTA (5) × (1 + 1 apoio)
            Assert.Equal("Pinheiros", relato.Regiao!.Nome);
            Assert.Equal(DateTimeKind.Utc, relato.CriadoEm.Kind);
            Assert.Equal("buraco", relato.Descricao!.Perigos.Single().Descricao);
            Assert.Equal(["poste", "lixeira"], relato.Descricao.Objetos);
            Assert.Equal(1, await db.Apoios.CountAsync(a => a.RelatoId == relatoId));
            Assert.Equal("Vistoria marcada", (await db.Atualizacoes.SingleAsync(a => a.RelatoId == relatoId)).Comentario);
            Assert.False((await db.Notificacoes.SingleAsync(n => n.RelatoId == relatoId)).Lida);
            Assert.Equal(201, (await db.Idempotencias.SingleAsync(i => i.Chave == "chave-1")).StatusHttp);
        }
    }

    [Fact(DisplayName = "gestores carregam as regiões que atendem (tabela GESTOR_REGIAO)")]
    public async Task GestorRegiao()
    {
        using var escopo = factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<VeAiDbContext>();

        var marina = await db.Usuarios.Include(u => u.Regioes).SingleAsync(u => u.Email == DadosTeste.EmailMarina);
        var pinheiros = await db.Regioes.SingleAsync(r => r.Nome == "Pinheiros");
        var vilaMariana = await db.Regioes.SingleAsync(r => r.Nome == "Vila Mariana");

        Assert.Equal(Papel.GESTOR, marina.Papel);
        Assert.True(marina.Atende(pinheiros.Id));
        Assert.False(marina.Atende(vilaMariana.Id));
    }
}
