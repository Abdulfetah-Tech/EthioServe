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
    public void Domain_depends_on_no_other_project_and_no_framework_libraries()
    {
        var referenced = ReferencedNames(DomainAssembly);

        Assert.DoesNotContain(referenced, n => n.StartsWith("EthioServe.", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_api_or_web_and_database_libraries()
    {
        var referenced = ReferencedNames(ApplicationAssembly);

        Assert.DoesNotContain(referenced, n => n == "EthioServe.Infrastructure");
        Assert.DoesNotContain(referenced, n => n == "EthioServe.API");
        Assert.DoesNotContain(referenced, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_api()
    {
        var referenced = ReferencedNames(InfrastructureAssembly);

        Assert.DoesNotContain(referenced, n => n == "EthioServe.API");
    }
}