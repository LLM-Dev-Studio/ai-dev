using AiDev.Features.Planning;

namespace AiDevNet.Tests.Unit;

public class SolutionDslValidatorTests
{
    // -------------------------------------------------------------------------
    // Valid documents
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_MinimalValidDocument_ReturnsValid()
    {
        var yaml = """
            version: "1.0"

            solution:
              name: MyApp
              business_dsl_ref: ./business.yaml

            projects:
              - name: MyApp.Api
                type: API
                description: REST API project.
            """;

        var result = SolutionDslValidator.Validate(yaml);

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_AllProjectTypesAndModules_ReturnsValid()
    {
        var yaml = """
            version: "1.0"

            solution:
              name: FullStack
              business_dsl_ref: ./business.yaml

            projects:
              - name: FullStack.Api
                type: API
                description: REST API.
              - name: FullStack.Infrastructure
                type: Infrastructure
                description: Data persistence.
              - name: FullStack.Contracts
                type: SharedContractsSDK
                description: Shared DTOs.
              - name: FullStack.Worker
                type: Worker
                description: Background jobs.
              - name: FullStack.Ui
                type: MauiHybridUI
                description: MAUI UI.

            modules:
              - name: Auth
                applies_to:
                  - FullStack.Api
              - name: CQRS
                applies_to:
                  - FullStack.Api
                  - FullStack.Worker
              - name: EFCore
                applies_to:
                  - FullStack.Infrastructure
              - name: Observability
                applies_to:
                  - FullStack.Api
                  - FullStack.Worker
                  - FullStack.Infrastructure
              - name: Validation
                applies_to:
                  - FullStack.Api
              - name: Caching
                applies_to:
                  - FullStack.Api
                  - FullStack.Infrastructure
              - name: Messaging
                applies_to:
                  - FullStack.Worker
                  - FullStack.Infrastructure
            """;

        var result = SolutionDslValidator.Validate(yaml);

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Invalid project type
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_UnsupportedProjectType_ReturnsError()
    {
        var yaml = """
            projects:
              - name: MyService
                type: Microservice
                description: Not in VSA stack.
            """;

        var result = SolutionDslValidator.Validate(yaml);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Rule == "INVALID_TYPE");
    }

    [Fact]
    public void Validate_MissingProjectType_ReturnsTypeRequiredError()
    {
        var yaml = """
            projects:
              - name: NoTypeProject
                description: Missing type field.
            """;

        var result = SolutionDslValidator.Validate(yaml);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Rule == "TYPE_REQUIRED");
    }

    [Fact]
    public void Validate_NoProjects_ReturnsNoProjectsError()
    {
        var yaml = """
            version: "1.0"
            solution:
              name: Empty
              business_dsl_ref: ./business.yaml
            """;

        var result = SolutionDslValidator.Validate(yaml);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Rule == "NO_PROJECTS");
    }

    // -------------------------------------------------------------------------
    // Invalid module name
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_UnsupportedModuleName_ReturnsError()
    {
        var yaml = """
            projects:
              - name: MyApp.Api
                type: API
                description: API.

            modules:
              - name: GraphQL
                applies_to:
                  - MyApp.Api
            """;

        var result = SolutionDslValidator.Validate(yaml);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Rule == "INVALID_MODULE");
    }

    // -------------------------------------------------------------------------
    // Compatibility rule violations
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_EFCoreAppliedToApi_ReturnsIncompatibleError()
    {
        var yaml = """
            projects:
              - name: MyApp.Api
                type: API
                description: REST API.

            modules:
              - name: EFCore
                applies_to:
                  - MyApp.Api
            """;

        var result = SolutionDslValidator.Validate(yaml);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Rule == "MODULE_INCOMPATIBLE" && e.Message.Contains("EFCore"));
    }

    [Fact]
    public void Validate_AuthAppliedToWorker_ReturnsIncompatibleError()
    {
        var yaml = """
            projects:
              - name: MyApp.Worker
                type: Worker
                description: Background worker.

            modules:
              - name: Auth
                applies_to:
                  - MyApp.Worker
            """;

        var result = SolutionDslValidator.Validate(yaml);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Rule == "MODULE_INCOMPATIBLE" && e.Message.Contains("Auth"));
    }

    [Fact]
    public void Validate_ModuleAppliedToSharedContractsSDK_ReturnsNoModulesAllowedError()
    {
        var yaml = """
            projects:
              - name: MyApp.Contracts
                type: SharedContractsSDK
                description: Shared DTOs.

            modules:
              - name: Observability
                applies_to:
                  - MyApp.Contracts
            """;

        var result = SolutionDslValidator.Validate(yaml);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Rule == "NO_MODULES_ALLOWED");
    }

    [Fact]
    public void Validate_ModuleAppliedToMauiHybridUI_ReturnsNoModulesAllowedError()
    {
        var yaml = """
            projects:
              - name: MyApp.Ui
                type: MauiHybridUI
                description: MAUI UI.

            modules:
              - name: CQRS
                applies_to:
                  - MyApp.Ui
            """;

        var result = SolutionDslValidator.Validate(yaml);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Rule == "NO_MODULES_ALLOWED");
    }

    // -------------------------------------------------------------------------
    // Edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_EmptyString_ReturnsEmptyDocumentError()
    {
        var result = SolutionDslValidator.Validate("");

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Rule == "EMPTY_DOCUMENT");
    }

    [Fact]
    public void Validate_ProjectTypeCaseInsensitive_ReturnsValid()
    {
        var yaml = """
            projects:
              - name: MyApp.Api
                type: api
                description: API project.
            """;

        var result = SolutionDslValidator.Validate(yaml);

        // Type matching is case-insensitive per the VSA taxonomy
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAllErrors()
    {
        var yaml = """
            projects:
              - name: MyApp.Api
                type: BadType
                description: Invalid type.
              - name: MyApp.Worker
                type: Worker
                description: Valid worker.

            modules:
              - name: BadModule
                applies_to:
                  - MyApp.Worker
              - name: EFCore
                applies_to:
                  - MyApp.Worker
            """;

        var result = SolutionDslValidator.Validate(yaml);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThan(1);
    }
}
