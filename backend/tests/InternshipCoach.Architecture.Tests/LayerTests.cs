using System.Reflection;
using NetArchTest.Rules;

namespace InternshipCoach.Architecture.Tests;

public class LayerTests
{
    private static readonly Assembly Domain = typeof(Domain.Common.CreatedAt).Assembly;
    private static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;

    [Fact]
    public void Domain_depends_on_no_other_layer()
    {
        var result = Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny("InternshipCoach.Application", "InternshipCoach.Infrastructure",
                "InternshipCoach.Api", "InternshipCoach.Contracts", "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny("InternshipCoach.Infrastructure", "InternshipCoach.Api", "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
