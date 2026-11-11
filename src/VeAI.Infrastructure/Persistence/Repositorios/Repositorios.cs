using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VeAI.Application.Abstracoes;
using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Regras;

namespace VeAI.Infrastructure.Persistence.Repositorios;

internal sealed class UsuarioRepositorio(VeAiDbContext db) : IUsuarioRepositorio
{
    public Task<Usuario?> BuscarPorEmailAsync(string emailNormalizado, CancellationToken ct = default) =>
        db.Usuarios.Include(u => u.Regioes).SingleOrDefaultAsync(u => u.Email == emailNormalizado, ct);

    public Task<Usuario?> BuscarComRegioesAsync(long id, CancellationToken ct = default) =>
        db.Usuarios.Include(u => u.Regioes).SingleOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> EmailExisteAsync(string emailNormalizado, CancellationToken ct = default) =>
        db.Usuarios.AnyAsync(u => u.Email == emailNormalizado, ct);

    public async Task<IReadOnlyList<long>> IdsDosGestoresDaRegiaoAsync(long regiaoId, CancellationToken ct = default) =>
        await db.Usuarios.Where(u => u.Papel == Papel.GESTOR && u.Regioes.Any(r => r.Id == regiaoId))
            .OrderBy(u => u.Id).Select(u => u.Id).ToListAsync(ct);

    public void Adicionar(Usuario usuario) => db.Usuarios.Add(usuario);
}

internal sealed class RegiaoRepositorio(VeAiDbContext db) : IRegiaoRepositorio
{
    public async Task<IReadOnlyList<Regiao>> ListarAsync(CancellationToken ct = default) =>
        await db.Regioes.OrderBy(r => r.Nome).ToListAsync(ct);

    public Task<Regiao?> BuscarAsync(long id, CancellationToken ct = default) =>
        db.Regioes.SingleOrDefaultAsync(r => r.Id == id, ct);
}

internal sealed class RelatoRepositorio(VeAiDbContext db) : IRelatoRepositorio
{
    private static readonly string[] NaoFinais = StatusRelatoExtensions.NaoFinais.Select(s => s.ToString()).ToArray();

    private bool Oracle => db.Database.ProviderName == "Oracle.EntityFrameworkCore";

    public Task<Relato?> BuscarAsync(long id, CancellationToken ct = default) =>
        db.Relatos.Include(r => r.Regiao).SingleOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Relato?> BuscarParaAtualizarAsync(long id, CancellationToken ct = default)
    {
        if (!Oracle)
            return await BuscarAsync(id, ct); // SQLite (testes) já serializa as escritas

        // ToList (e não SingleOrDefault): o EF não pode envolver o FOR UPDATE num subselect, o Oracle recusa.
        var relato = (await db.Relatos.FromSqlInterpolated($"SELECT * FROM RELATO WHERE ID = {id} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault();
        if (relato is not null)
            await db.Entry(relato).Reference(r => r.Regiao).LoadAsync(ct);
        return relato;
    }

    public async Task<IReadOnlyList<Relato>> CandidatosProximosAsync(TipoRelato tipo, double latMin, double latMax,
        double lonMin, double lonMax, CancellationToken ct = default)
    {
        List<Relato> candidatos;
        if (Oracle)
        {
            // FOR UPDATE: dois envios ao mesmo tempo para o mesmo lugar esperam um pelo outro,
            // então nenhum apoio se perde e não nascem dois relatos iguais.
            var tipoTexto = tipo.ToString();
            candidatos = await db.Relatos.FromSqlInterpolated($"""
                SELECT * FROM RELATO
                 WHERE TIPO = {tipoTexto}
                   AND STATUS IN ({NaoFinais[0]}, {NaoFinais[1]}, {NaoFinais[2]})
                   AND LATITUDE BETWEEN {latMin} AND {latMax}
                   AND LONGITUDE BETWEEN {lonMin} AND {lonMax}
                 FOR UPDATE
                """).ToListAsync(ct);
            foreach (var r in candidatos)
                await db.Entry(r).Reference(x => x.Regiao).LoadAsync(ct);
        }
        else
        {
            var status = StatusRelatoExtensions.NaoFinais.ToList();
            candidatos = await db.Relatos.Include(r => r.Regiao)
                .Where(r => r.Tipo == tipo && status.Contains(r.Status)
                            && r.Latitude >= latMin && r.Latitude <= latMax
                            && r.Longitude >= lonMin && r.Longitude <= lonMax)
                .ToListAsync(ct);
        }
        return candidatos;
    }

    public Task<Pagina<Relato>> DoCidadaoAsync(long cidadaoId, int pagina, int tamanho, CancellationToken ct = default) =>
        Paginar(db.Relatos.AsNoTracking().Include(r => r.Regiao).Where(r => r.CidadaoId == cidadaoId)
            .OrderByDescending(r => r.CriadoEm).ThenByDescending(r => r.Id), pagina, tamanho, ct);

    public Task<Pagina<Relato>> FilaAsync(IReadOnlyCollection<long> regioes, StatusRelato? status, int pagina, int tamanho,
                                          CancellationToken ct = default)
    {
        var ids = regioes.ToList();
        var consulta = db.Relatos.AsNoTracking().Include(r => r.Regiao).Where(r => ids.Contains(r.RegiaoId));
        if (status is { } s)
            consulta = consulta.Where(r => r.Status == s);
        // O id desempata: no Oracle as datas são DATE (precisão de segundos), então empates são comuns.
        return Paginar(consulta.OrderByDescending(r => r.Prioridade).ThenBy(r => r.CriadoEm).ThenBy(r => r.Id),
            pagina, tamanho, ct);
    }

    public async Task<IReadOnlyList<Relato>> NaoFinalizadosAsync(CancellationToken ct = default)
    {
        var status = StatusRelatoExtensions.NaoFinais.ToList();
        return await db.Relatos.Where(r => status.Contains(r.Status)).ToListAsync(ct);
    }

    public void Adicionar(Relato relato) => db.Relatos.Add(relato);

    public void Remover(Relato relato) => db.Relatos.Remove(relato);

    private static async Task<Pagina<Relato>> Paginar(IQueryable<Relato> consulta, int pagina, int tamanho, CancellationToken ct)
    {
        var total = await consulta.LongCountAsync(ct);
        var itens = await consulta.Skip(pagina * tamanho).Take(tamanho).ToListAsync(ct);
        return new Pagina<Relato>(itens, total);
    }
}

internal sealed class ApoioRepositorio(VeAiDbContext db) : IApoioRepositorio
{
    public Task<bool> ExisteAsync(long relatoId, long usuarioId, CancellationToken ct = default) =>
        db.Apoios.AnyAsync(a => a.RelatoId == relatoId && a.UsuarioId == usuarioId, ct);

    public async Task<IReadOnlyList<long>> IdsDosApoiadoresAsync(long relatoId, CancellationToken ct = default) =>
        await db.Apoios.Where(a => a.RelatoId == relatoId).OrderBy(a => a.Id).Select(a => a.UsuarioId).ToListAsync(ct);

    public void Adicionar(ApoioRelato apoio) => db.Apoios.Add(apoio);
}

internal sealed class AtualizacaoRepositorio(VeAiDbContext db) : IAtualizacaoRepositorio
{
    public async Task<IReadOnlyList<AtualizacaoRelato>> HistoricoAsync(long relatoId, CancellationToken ct = default) =>
        await db.Atualizacoes.AsNoTracking().Include(a => a.Gestor).Where(a => a.RelatoId == relatoId)
            .OrderBy(a => a.CriadoEm).ThenBy(a => a.Id).ToListAsync(ct);

    public void Adicionar(AtualizacaoRelato atualizacao) => db.Atualizacoes.Add(atualizacao);
}

internal sealed class DescricaoRepositorio(VeAiDbContext db) : IDescricaoRepositorio
{
    public Task<Descricao?> BuscarDoUsuarioAsync(long id, long usuarioId, CancellationToken ct = default) =>
        db.Descricoes.SingleOrDefaultAsync(d => d.Id == id && d.UsuarioId == usuarioId, ct);

    public async Task<Pagina<Descricao>> DoUsuarioAsync(long usuarioId, int pagina, int tamanho, CancellationToken ct = default)
    {
        var consulta = db.Descricoes.AsNoTracking().Where(d => d.UsuarioId == usuarioId)
            .OrderByDescending(d => d.CriadoEm).ThenByDescending(d => d.Id);
        var total = await consulta.LongCountAsync(ct);
        return new Pagina<Descricao>(await consulta.Skip(pagina * tamanho).Take(tamanho).ToListAsync(ct), total);
    }

    public void Adicionar(Descricao descricao) => db.Descricoes.Add(descricao);

    public void Remover(Descricao descricao) => db.Descricoes.Remove(descricao);
}

internal sealed class NotificacaoRepositorio(VeAiDbContext db) : INotificacaoRepositorio
{
    public async Task<Pagina<Notificacao>> DoUsuarioAsync(long usuarioId, bool? lida, int pagina, int tamanho, CancellationToken ct = default)
    {
        var consulta = db.Notificacoes.AsNoTracking().Where(n => n.UsuarioId == usuarioId);
        if (lida is { } l)
            consulta = consulta.Where(n => n.Lida == l);
        consulta = consulta.OrderByDescending(n => n.CriadoEm).ThenByDescending(n => n.Id);
        var total = await consulta.LongCountAsync(ct);
        return new Pagina<Notificacao>(await consulta.Skip(pagina * tamanho).Take(tamanho).ToListAsync(ct), total);
    }

    public Task<Notificacao?> BuscarDoUsuarioAsync(long id, long usuarioId, CancellationToken ct = default) =>
        db.Notificacoes.SingleOrDefaultAsync(n => n.Id == id && n.UsuarioId == usuarioId, ct);

    public void Adicionar(Notificacao notificacao) => db.Notificacoes.Add(notificacao);
}

internal sealed class IdempotenciaRepositorio(VeAiDbContext db) : IIdempotenciaRepositorio
{
    public Task<RegistroIdempotencia?> BuscarAsync(long usuarioId, string chave, CancellationToken ct = default) =>
        db.Idempotencias.SingleOrDefaultAsync(i => i.UsuarioId == usuarioId && i.Chave == chave, ct);

    public void Adicionar(RegistroIdempotencia registro) => db.Idempotencias.Add(registro);

    public void Remover(RegistroIdempotencia registro) => db.Idempotencias.Remove(registro);

    public Task<int> ApagarAnterioresAAsync(DateTime limite, CancellationToken ct = default) =>
        db.Idempotencias.Where(i => i.CriadoEm < limite).ExecuteDeleteAsync(ct);
}

/// <summary>
/// SaveChanges + transação explícita. Ações agendadas com AposConfirmar (ex.: publicar no RabbitMQ)
/// só rodam depois do commit, para nunca avisar sobre algo que foi desfeito.
/// </summary>
internal sealed class UnidadeDeTrabalho(VeAiDbContext db) : IUnidadeDeTrabalho
{
    private readonly List<Func<Task>> _aposConfirmar = [];
    private IDbContextTransaction? _transacao;

    public async Task SalvarAsync(CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
        if (_transacao is null)
            await DispararAsync();
    }

    public async Task<T> EmTransacaoAsync<T>(Func<Task<T>> acao, CancellationToken ct = default)
    {
        if (_transacao is not null)
            return await acao(); // já dentro de uma transação: participa dela

        T resultado;
        await using (var transacao = await db.Database.BeginTransactionAsync(ct))
        {
            _transacao = transacao;
            try
            {
                resultado = await acao();
                await db.SaveChangesAsync(ct);
                await transacao.CommitAsync(ct);
            }
            catch
            {
                await transacao.RollbackAsync(CancellationToken.None);
                _aposConfirmar.Clear();
                db.ChangeTracker.Clear(); // nada do que foi desfeito pode ser regravado depois
                throw;
            }
            finally
            {
                _transacao = null;
            }
        }

        await DispararAsync(); // só depois do commit
        return resultado;
    }

    public void AposConfirmar(Func<Task> acao) => _aposConfirmar.Add(acao);

    private async Task DispararAsync()
    {
        var acoes = _aposConfirmar.ToList();
        _aposConfirmar.Clear();
        foreach (var acao in acoes)
            await acao();
    }
}
