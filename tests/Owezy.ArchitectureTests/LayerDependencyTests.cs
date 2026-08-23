using NetArchTest.Rules;
using Xunit;

namespace Owezy.ArchitectureTests;

public class LayerDependencyTests
{
    private const string DomainNamespace = "Owezy.Domain";
    private const string ApplicationNamespace = "Owezy.Application";
    private const string InfrastructureNamespace = "Owezy.Infrastructure";
    private const string ApiNamespace = "Owezy.Api";

    [Fact]
    public void Domain_Should_Not_DependOn_Other_Layers()
    {
        var result = Types.InAssembly(typeof(Owezy.Domain.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain layer must not depend on Application, Infrastructure, or Api layers.");
    }

    [Fact]
    public void Application_Should_Not_DependOn_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(typeof(Owezy.Application.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Application layer must not depend on Infrastructure or Api layers.");
    }
}
