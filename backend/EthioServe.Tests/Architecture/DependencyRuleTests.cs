using System.Reflection;

namespace EthioServe.Tests.Architecture;

public sealed class DependencyRuleTests
{
    private static readonly Assembly DomainAssembly = typeof(EthioServe.Domain.Common.DomainException).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(EthioServe.Application.DependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(EthioServe.Infrastructure.DependencyInjection).Assembly;

    private static List<string> ReferencedNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();

       [Fact]
    public void Application_does_not_depend_on_infrastructure_api_or_web_and_provider_libraries()
    {
        var referenced = ReferencedNames(ApplicationAssembly);

        // EF Core itself is allowed here (IApplicationDbContext); the database provider and the web stack are not.
        Assert.DoesNotContain(referenced, n => n == "EthioServe.Infrastructure");
        Assert.DoesNotContain(referenced, n => n == "EthioServe.API");
        Assert.DoesNotContain(referenced, n => n.StartsWith("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

  
    [Fact]
    public void Infrastructure_does_not_depend_on_the_api()
    {
        var referenced = ReferencedNames(InfrastructureAssembly);

        Assert.DoesNotContain(referenced, n => n == "EthioServe.API");
    }
}