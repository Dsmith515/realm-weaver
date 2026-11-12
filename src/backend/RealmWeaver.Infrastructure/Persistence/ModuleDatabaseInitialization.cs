using Microsoft.Extensions.DependencyInjection;

namespace RealmWeaver.Infrastructure.Persistence;

/// <summary>Applies one module's migrations and seed data. Must be safe to run on every startup.</summary>
public interface IModuleDatabaseInitializer
{
    /// <summary>Initializers run in ascending order, so a module can seed through another module's contract.</summary>
    int Order { get; }

    Task InitializeAsync(CancellationToken cancellationToken);
}

public static class ModuleDatabaseInitialization
{
    public static async Task InitializeModuleDatabasesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var initializers = scope.ServiceProvider.GetServices<IModuleDatabaseInitializer>().OrderBy(i => i.Order);
        foreach (var initializer in initializers)
        {
            await initializer.InitializeAsync(cancellationToken);
        }
    }
}
