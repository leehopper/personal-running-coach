using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using NSubstitute;
using RunCoach.Api.Swashbuckle;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RunCoach.Api.Tests.Swashbuckle;

/// <summary>
/// Unit coverage for <see cref="AuthorizeOperationFilter"/> across the three
/// behavioural branches: authorize present, allow-anonymous present, and
/// neither attribute present.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuthorizeOperationFilterTests
{
    private readonly AuthorizeOperationFilter _sut = new();

    [Fact]
    public void Apply_AddsRequirement_WhenAuthorizePresent()
    {
        // Arrange — action marked with [Authorize]; document carries both scheme definitions.
        var operation = new OpenApiOperation();
        var context = BuildContext(
            typeof(AuthorizedController),
            typeof(AuthorizedController).GetMethod(nameof(AuthorizedController.Action))!,
            withSecuritySchemes: true);

        // Act
        _sut.Apply(operation, context);

        // Assert — the CookieOrBearer policy is OR semantics, so each scheme is emitted as its
        // OWN requirement object (separate objects are OR-ed; schemes within one object are AND-ed).
        operation.Security.Should().NotBeNullOrEmpty();
        operation.Security.Should().HaveCount(2);

        operation.Security
            .Select(requirement => requirement.Keys.Single().Reference.Id)
            .Should().BeEquivalentTo("cookieAuth", "bearerAuth");
        operation.Security.Should().AllSatisfy(requirement =>
            requirement.Values.Should().AllSatisfy(scopes => scopes.Should().BeEmpty()));
    }

    [Fact]
    public void Apply_SkipsRequirement_WhenAllowAnonymousPresent()
    {
        // Arrange — controller marked with [AllowAnonymous] (and no [Authorize]).
        var operation = new OpenApiOperation();
        var context = BuildContext(
            typeof(AnonymousController),
            typeof(AnonymousController).GetMethod(nameof(AnonymousController.Action))!,
            withSecuritySchemes: false);

        // Act
        _sut.Apply(operation, context);

        // Assert — no security requirement added.
        operation.Security.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Apply_DoesNothing_WhenNeitherAttributePresent()
    {
        // Arrange — controller with no auth attributes.
        var operation = new OpenApiOperation();
        var context = BuildContext(
            typeof(UndecoratedController),
            typeof(UndecoratedController).GetMethod(nameof(UndecoratedController.Action))!,
            withSecuritySchemes: false);

        // Act
        _sut.Apply(operation, context);

        // Assert
        operation.Security.Should().BeNullOrEmpty();
    }

    /// <summary>
    /// Builds an <see cref="OperationFilterContext"/> using a
    /// <see cref="ControllerActionDescriptor"/> so that
    /// <c>ApiDescriptionExtensions.CustomAttributes()</c> reads the stub controller's
    /// type-level attributes via <c>ControllerTypeInfo</c>. A plain
    /// <see cref="Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor"/> does not
    /// expose <c>ControllerTypeInfo</c>, which is the primary attribute source
    /// Swashbuckle reads in production.
    /// </summary>
    /// <param name="controllerType">The stub controller type whose class-level attributes carry the auth metadata.</param>
    /// <param name="method">The action method on the stub controller.</param>
    /// <param name="withSecuritySchemes">
    /// When <see langword="true"/>, pre-registers the <c>cookieAuth</c> and <c>bearerAuth</c>
    /// schemes in the document so the filter can build properly-resolved
    /// <see cref="OpenApiSecuritySchemeReference"/> keys.
    /// </param>
    private static OperationFilterContext BuildContext(
        Type controllerType,
        MethodInfo method,
        bool withSecuritySchemes)
    {
        var apiDescription = new ApiDescription
        {
            ActionDescriptor = new ControllerActionDescriptor
            {
                ControllerTypeInfo = controllerType.GetTypeInfo(),
                MethodInfo = method,
            },
        };

        var document = new OpenApiDocument();
        if (withSecuritySchemes)
        {
            // Mirrors the AddSecurityDefinition calls in Program.cs so the filter
            // can resolve the scheme names into host-document-bound references.
            document.Components = new OpenApiComponents
            {
                SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["cookieAuth"] = new OpenApiSecuritySchemeReference("cookieAuth", document),
                    ["bearerAuth"] = new OpenApiSecuritySchemeReference("bearerAuth", document),
                },
            };
        }

        var schemaGenerator = Substitute.For<ISchemaGenerator>();
        var schemaRepository = new SchemaRepository();

        return new OperationFilterContext(apiDescription, schemaGenerator, schemaRepository, document, method);
    }

    // Inline stub controllers — attribute carriers only.
    [Authorize]
    private static class AuthorizedController
    {
        public static void Action()
        {
            // Stub — exists only to carry [Authorize] as controller-level metadata.
        }
    }

    [AllowAnonymous]
    private static class AnonymousController
    {
        public static void Action()
        {
            // Stub — exists only to carry [AllowAnonymous] as controller-level metadata.
        }
    }

    private static class UndecoratedController
    {
        public static void Action()
        {
            // Stub — exists only to provide a controller type with no auth attributes.
        }
    }
}
