
namespace RealmWeaver.Generation.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Generation_library_stays_pure()
    {
        var referenced = typeof(RealmGenerator).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(referenced, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, n => n.StartsWith("RealmWeaver.", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, n => n.StartsWith("Npgsql", StringComparison.Ordinal));
    }
}
