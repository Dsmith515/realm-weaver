using System.Data;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace RealmWeaver.Infrastructure.Persistence;

/// <summary>
/// Runs work from several modules in one Postgres transaction. All module DbContexts in a DI scope share the
/// scope's <see cref="NpgsqlConnection"/>; while a transaction is open, every one of them is enlisted in it.
/// This is the Phase 1 "local ACID" baseline that the Phase 2 saga replaces (ADR 0001, ADR 0005).
/// </summary>
public sealed class UnitOfWork(NpgsqlConnection connection)
{
    private readonly List<DbContext> contexts = [];
    private NpgsqlTransaction? transaction;

    public bool IsInTransaction => transaction is not null;

    internal void Attach(DbContext context)
    {
        contexts.Add(context);
        if (transaction is not null)
        {
            context.Database.UseTransaction(transaction);
        }
    }

    /// <summary>
    /// Commits when <paramref name="work"/> returns and rolls everything back when it throws.
    /// Tracked changes in the attached contexts are discarded on rollback, so the scope stays usable.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (transaction is not null)
        {
            throw new InvalidOperationException("A unit of work is already running in this scope.");
        }

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            foreach (var context in contexts)
            {
                await context.Database.UseTransactionAsync(transaction, cancellationToken);
            }

            var result = await work(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            // CancellationToken.None: the rollback must happen even if the request was aborted.
            await transaction.RollbackAsync(CancellationToken.None);
            foreach (var context in contexts)
            {
                context.ChangeTracker.Clear();
            }

            throw;
        }
        finally
        {
            foreach (var context in contexts)
            {
                await context.Database.UseTransactionAsync(null, CancellationToken.None);
            }

            await transaction.DisposeAsync();
            transaction = null;
        }
    }
}
