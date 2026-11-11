using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeAI.Domain.Entities;
using VeAI.Domain.Modelos;

namespace VeAI.Infrastructure.Persistence.Configuracoes;

// Nomes em MAIÚSCULAS: no Oracle, tabelas criadas sem aspas ficam em maiúsculas,
// e o provedor do EF Core usa aspas nos identificadores.

internal sealed class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("USUARIO");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(u => u.Nome).HasColumnName("NOME").HasMaxLength(120).IsRequired();
        b.Property(u => u.Email).HasColumnName("EMAIL").HasMaxLength(160).IsRequired();
        b.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UK_USUARIO_EMAIL");
        b.Property(u => u.SenhaHash).HasColumnName("SENHA_HASH").HasMaxLength(100).IsRequired();
        b.Property(u => u.Papel).HasColumnName("PAPEL").HasConversion<string>().HasMaxLength(10);
        b.Property(u => u.VelocidadeVoz).HasColumnName("VELOCIDADE_VOZ").HasColumnType("NUMBER(3,2)");
        b.Property(u => u.PerguntaLivreAtiva).HasColumnName("PERGUNTA_LIVRE_ATIVA").HasColumnType("NUMBER(1)");
        b.Property(u => u.CriadoEm).HasColumnName("CRIADO_EM");

        // Tabela GESTOR_REGIAO (muitos-para-muitos sem classe própria).
        b.HasMany(u => u.Regioes).WithMany().UsingEntity<Dictionary<string, object>>(
            "GESTOR_REGIAO",
            r => r.HasOne<Regiao>().WithMany().HasForeignKey("REGIAO_ID"),
            g => g.HasOne<Usuario>().WithMany().HasForeignKey("GESTOR_ID"),
            j =>
            {
                j.ToTable("GESTOR_REGIAO");
                j.HasKey("GESTOR_ID", "REGIAO_ID");
            });
    }
}

internal sealed class RegiaoConfig : IEntityTypeConfiguration<Regiao>
{
    public void Configure(EntityTypeBuilder<Regiao> b)
    {
        b.ToTable("REGIAO");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(r => r.Nome).HasColumnName("NOME").HasMaxLength(80).IsRequired();
        b.Property(r => r.Cidade).HasColumnName("CIDADE").HasMaxLength(80).IsRequired();
        b.Property(r => r.Uf).HasColumnName("UF").HasMaxLength(2).IsFixedLength().IsRequired();
        b.Property(r => r.Latitude).HasColumnName("LATITUDE").HasColumnType("NUMBER(9,6)");
        b.Property(r => r.Longitude).HasColumnName("LONGITUDE").HasColumnType("NUMBER(9,6)");
        b.Ignore(r => r.TemCentro);
    }
}

internal sealed class DescricaoConfig : IEntityTypeConfiguration<Descricao>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Descricao> b)
    {
        b.ToTable("DESCRICAO");
        b.HasKey(d => d.Id);
        b.Property(d => d.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(d => d.UsuarioId).HasColumnName("USUARIO_ID");
        b.HasOne(d => d.Usuario).WithMany().HasForeignKey(d => d.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.Property(d => d.Modo).HasColumnName("MODO").HasConversion<string>().HasMaxLength(10);
        b.Property(d => d.Resumo).HasColumnName("RESUMO").HasMaxLength(500).IsRequired();
        b.Property(d => d.DescricaoCompleta).HasColumnName("DESCRICAO_COMPLETA").HasColumnType("CLOB");

        // Listas gravadas como JSON em CLOB (CHECK IS JSON no Oracle), como na API Java.
        b.Property(d => d.Perigos).HasColumnName("PERIGOS").HasColumnType("CLOB")
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<Perigo>>(v, Json) ?? new List<Perigo>(),
                new ValueComparer<List<Perigo>>(
                    (a, c) => a!.SequenceEqual(c!), v => v.Aggregate(0, (h, p) => HashCode.Combine(h, p)), v => v.ToList()));
        b.Property(d => d.Objetos).HasColumnName("OBJETOS").HasColumnType("CLOB")
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<string>>(v, Json) ?? new List<string>(),
                new ValueComparer<List<string>>(
                    (a, c) => a!.SequenceEqual(c!), v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s)), v => v.ToList()));

        b.Property(d => d.BarreiraDetectada).HasColumnName("BARREIRA_DETECTADA").HasColumnType("NUMBER(1)");
        b.Property(d => d.BarreiraTipo).HasColumnName("BARREIRA_TIPO").HasConversion<string>().HasMaxLength(30);
        b.Property(d => d.LatenciaMs).HasColumnName("LATENCIA_MS").HasColumnType("NUMBER(6)");
        b.Property(d => d.Util).HasColumnName("UTIL").HasColumnType("NUMBER(1)");
        b.Property(d => d.CriadoEm).HasColumnName("CRIADO_EM");
    }
}

internal sealed class RelatoConfig : IEntityTypeConfiguration<Relato>
{
    public void Configure(EntityTypeBuilder<Relato> b)
    {
        b.ToTable("RELATO");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(r => r.CidadaoId).HasColumnName("CIDADAO_ID");
        b.HasOne(r => r.Cidadao).WithMany().HasForeignKey(r => r.CidadaoId).OnDelete(DeleteBehavior.Restrict);
        b.Property(r => r.DescricaoId).HasColumnName("DESCRICAO_ID");
        b.HasOne(r => r.Descricao).WithMany().HasForeignKey(r => r.DescricaoId).OnDelete(DeleteBehavior.SetNull);
        b.Property(r => r.RegiaoId).HasColumnName("REGIAO_ID");
        b.HasOne(r => r.Regiao).WithMany().HasForeignKey(r => r.RegiaoId).OnDelete(DeleteBehavior.Restrict);
        b.Property(r => r.Tipo).HasColumnName("TIPO").HasConversion<string>().HasMaxLength(30);
        b.Property(r => r.Gravidade).HasColumnName("GRAVIDADE").HasConversion<string>().HasMaxLength(5);
        b.Property(r => r.Texto).HasColumnName("TEXTO").HasMaxLength(1000).IsRequired();
        b.Property(r => r.Latitude).HasColumnName("LATITUDE").HasColumnType("NUMBER(9,6)");
        b.Property(r => r.Longitude).HasColumnName("LONGITUDE").HasColumnType("NUMBER(9,6)");
        b.Property(r => r.Endereco).HasColumnName("ENDERECO").HasMaxLength(200);
        b.Property(r => r.Status).HasColumnName("STATUS").HasConversion<string>().HasMaxLength(12);
        b.Property(r => r.ProtocoloOrgao).HasColumnName("PROTOCOLO_ORGAO").HasMaxLength(40);
        b.Property(r => r.MotivoDescarte).HasColumnName("MOTIVO_DESCARTE").HasMaxLength(300);
        b.Property(r => r.TotalApoios).HasColumnName("TOTAL_APOIOS").HasColumnType("NUMBER(6)");
        b.Property(r => r.Prioridade).HasColumnName("PRIORIDADE").HasColumnType("NUMBER(10,2)");
        b.Property(r => r.NormaReferencia).HasColumnName("NORMA_REFERENCIA").HasMaxLength(200);
        b.Property(r => r.CriadoEm).HasColumnName("CRIADO_EM");
        b.Property(r => r.AtualizadoEm).HasColumnName("ATUALIZADO_EM");
        b.HasIndex(r => new { r.RegiaoId, r.Status, r.Prioridade }).HasDatabaseName("IX_RELATO_REGIAO_STATUS");
        b.HasIndex(r => new { r.Tipo, r.Status, r.Latitude, r.Longitude }).HasDatabaseName("IX_RELATO_TIPO_GEO");

        // A tabela tem gatilhos (trg_auditoria_relato e trg_conta_apoio).
        b.ToTable(t => t.HasTrigger("TRG_AUDITORIA_RELATO"));
    }
}

internal sealed class ApoioRelatoConfig : IEntityTypeConfiguration<ApoioRelato>
{
    public void Configure(EntityTypeBuilder<ApoioRelato> b)
    {
        b.ToTable("APOIO_RELATO", t => t.HasTrigger("TRG_CONTA_APOIO"));
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(a => a.RelatoId).HasColumnName("RELATO_ID");
        b.Property(a => a.UsuarioId).HasColumnName("USUARIO_ID");
        b.Property(a => a.CriadoEm).HasColumnName("CRIADO_EM");
        b.HasOne<Relato>().WithMany().HasForeignKey(a => a.RelatoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(a => a.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.RelatoId, a.UsuarioId }).IsUnique().HasDatabaseName("UK_APOIO_RELATO");
    }
}

internal sealed class AtualizacaoRelatoConfig : IEntityTypeConfiguration<AtualizacaoRelato>
{
    public void Configure(EntityTypeBuilder<AtualizacaoRelato> b)
    {
        b.ToTable("ATUALIZACAO_RELATO");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(a => a.RelatoId).HasColumnName("RELATO_ID");
        b.Property(a => a.GestorId).HasColumnName("GESTOR_ID");
        b.HasOne<Relato>().WithMany().HasForeignKey(a => a.RelatoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(a => a.Gestor).WithMany().HasForeignKey(a => a.GestorId).OnDelete(DeleteBehavior.Restrict);
        b.Property(a => a.StatusAnterior).HasColumnName("STATUS_ANTERIOR").HasConversion<string>().HasMaxLength(12);
        b.Property(a => a.StatusNovo).HasColumnName("STATUS_NOVO").HasConversion<string>().HasMaxLength(12);
        b.Property(a => a.Comentario).HasColumnName("COMENTARIO").HasMaxLength(500);
        b.Property(a => a.CriadoEm).HasColumnName("CRIADO_EM");
    }
}

internal sealed class NotificacaoConfig : IEntityTypeConfiguration<Notificacao>
{
    public void Configure(EntityTypeBuilder<Notificacao> b)
    {
        b.ToTable("NOTIFICACAO");
        b.HasKey(n => n.Id);
        b.Property(n => n.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(n => n.UsuarioId).HasColumnName("USUARIO_ID");
        b.Property(n => n.RelatoId).HasColumnName("RELATO_ID");
        b.HasOne<Usuario>().WithMany().HasForeignKey(n => n.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Relato>().WithMany().HasForeignKey(n => n.RelatoId).OnDelete(DeleteBehavior.Cascade);
        b.Property(n => n.Mensagem).HasColumnName("MENSAGEM").HasMaxLength(300).IsRequired();
        b.Property(n => n.Lida).HasColumnName("LIDA").HasColumnType("NUMBER(1)");
        b.Property(n => n.CriadoEm).HasColumnName("CRIADO_EM");
    }
}

internal sealed class RegistroIdempotenciaConfig : IEntityTypeConfiguration<RegistroIdempotencia>
{
    public void Configure(EntityTypeBuilder<RegistroIdempotencia> b)
    {
        b.ToTable("IDEMPOTENCIA");
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(i => i.UsuarioId).HasColumnName("USUARIO_ID");
        b.HasOne<Usuario>().WithMany().HasForeignKey(i => i.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        b.Property(i => i.Chave).HasColumnName("CHAVE").HasMaxLength(100).IsRequired();
        b.HasIndex(i => new { i.UsuarioId, i.Chave }).IsUnique().HasDatabaseName("UK_IDEMPOTENCIA_USUARIO_CHAVE");
        b.Property(i => i.HashRequisicao).HasColumnName("HASH_REQUISICAO").HasMaxLength(64).IsRequired();
        b.Property(i => i.StatusHttp).HasColumnName("STATUS_HTTP").HasColumnType("NUMBER(3)");
        b.Property(i => i.CorpoResposta).HasColumnName("CORPO_RESPOSTA").HasColumnType("NCLOB").IsRequired();
        b.Property(i => i.CriadoEm).HasColumnName("CRIADO_EM");
    }
}
