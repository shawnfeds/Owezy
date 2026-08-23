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
    public void Domain_MustNot_DependOn_Application_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(typeof(Owezy.Domain.Auth.User).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain layer must not depend on Application, Infrastructure, or Api layers.");
    }

    [Fact]
    public void Application_MustNot_DependOn_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(typeof(Owezy.Application.Auth.IUserRepository).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Application layer must not depend on Infrastructure or Api layers.");
    }

    [Fact]
    public void Infrastructure_MustNot_DependOn_Api()
    {
        var result = Types.InAssembly(typeof(Owezy.Infrastructure.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Infrastructure layer must not depend on Api layer.");
    }
}
