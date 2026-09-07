using System.Reflection;
using NetArchTest.Rules;
using RoomRaah.Application;
using RoomRaah.Domain.Exceptions;
using RoomRaah.Infrastructure.Persistence;
using Shouldly;

namespace RoomRaah.UnitTests.Architecture;

public class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(NotFoundException).Assembly;
    private static readonly Assembly Application = typeof(IApplicationAssemblyMarker).Assembly;
    private static readonly Assembly Infrastructure = typeof(RoomRaahDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private const string DomainNs = "RoomRaah.Domain";
    private const string ApplicationNs = "RoomRaah.Application";
    private const string InfrastructureNs = "RoomRaah.Infrastructure";
    private const string ApiNs = "RoomRaah.API";

    [Fact]
    public void Domain_should_not_depend_on_any_other_layer()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, ApiNs)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Domain_should_have_no_third_party_dependencies()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "MediatR",
                "AutoMapper",
                "FluentValidation",
                "StackExchange.Redis",
                "Azure",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Application_should_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, ApiNs)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Application_should_not_depend_on_persistence_or_cloud_packages()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "StackExchange.Redis",
                "Azure",
                "MailKit",
                "SixLabors",
                "SkiaSharp")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_api()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOn(ApiNs)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Controllers_should_not_depend_on_infrastructure()
    {
        var result = Types.InAssembly(Api)
            .That()
            .ResideInNamespaceStartingWith($"{ApiNs}.Controllers")
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNs)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    private static string Describe(TestResult result)
    {
        var names = result.FailingTypeNames is null
            ? "unknown"
            : string.Join(", ", result.FailingTypeNames);

        return $"Dependency rule violated by: {names}";
    }
}
