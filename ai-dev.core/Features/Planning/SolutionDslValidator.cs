namespace AiDev.Features.Planning;

/// <summary>
/// Validates a Solution.dsl YAML document against the VSA stack taxonomy rules (AD-10).
///
/// Validated rules:
/// - Each project.type is one of the 5 supported values.
/// - Each module.name is one of the 7 supported values.
/// - MauiHybridUI and SharedContractsSDK projects have no modules.
/// - EFCore only applies to Infrastructure.
/// - Auth only applies to API.
/// - CQRS applies only to API or Worker.
/// - Observability applies to API, Worker, or Infrastructure.
/// - Validation applies to API or Worker.
/// - Caching applies to API or Infrastructure.
/// - Messaging applies to Worker or Infrastructure.
/// - At least one project must be defined.
/// </summary>
public static class SolutionDslValidator
{
    // -------------------------------------------------------------------------
    // VSA stack constants (authoritative source: kb/vsa-stack-taxonomy.md)
    // -------------------------------------------------------------------------

    private static readonly HashSet<string> ValidProjectTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "API", "Infrastructure", "SharedContractsSDK", "MauiHybridUI", "Worker",
    };

    private static readonly HashSet<string> ValidModuleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Auth", "CQRS", "EFCore", "Observability", "Validation", "Caching", "Messaging",
    };

    /// <summary>Module → set of project types it may be applied to.</summary>
    private static readonly Dictionary<string, HashSet<string>> ModuleCompatibility =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Auth"]         = new(StringComparer.OrdinalIgnoreCase) { "API" },
            ["CQRS"]         = new(StringComparer.OrdinalIgnoreCase) { "API", "Worker" },
            ["EFCore"]       = new(StringComparer.OrdinalIgnoreCase) { "Infrastructure" },
            ["Observability"]= new(StringComparer.OrdinalIgnoreCase) { "API", "Worker", "Infrastructure" },
            ["Validation"]   = new(StringComparer.OrdinalIgnoreCase) { "API", "Worker" },
            ["Caching"]      = new(StringComparer.OrdinalIgnoreCase) { "API", "Infrastructure" },
            ["Messaging"]    = new(StringComparer.OrdinalIgnoreCase) { "Worker", "Infrastructure" },
        };

    /// <summary>Project types that must have zero modules.</summary>
    private static readonly HashSet<string> NoModuleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SharedContractsSDK", "MauiHybridUI",
    };

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Validates <paramref name="yaml"/> against the Solution.dsl schema and VSA compatibility rules.
    /// Returns <see cref="SolutionDslValidationResult.Valid()"/> when no violations are found.
    /// </summary>
    public static SolutionDslValidationResult Validate(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return SolutionDslValidationResult.Invalid(
            [
                new("EMPTY_DOCUMENT", "Solution.dsl is empty or whitespace."),
            ]);

        var errors = new List<SolutionDslValidationError>();
        var (projects, modules) = ParseSolutionDsl(yaml);

        // ---- Require at least one project ----
        if (projects.Count == 0)
        {
            errors.Add(new("NO_PROJECTS", "Solution.dsl must define at least one project."));
        }

        // ---- Build project-type lookup ----
        var projectTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in projects)
        {
            if (string.IsNullOrWhiteSpace(p.Type))
            {
                errors.Add(new("TYPE_REQUIRED",
                    $"Project '{p.Name}': 'type' field is required."));
                continue;
            }

            if (!ValidProjectTypes.Contains(p.Type))
            {
                errors.Add(new("INVALID_TYPE",
                    $"Project '{p.Name}': type '{p.Type}' is not a supported project type. " +
                    $"Supported types: {string.Join(", ", ValidProjectTypes)}."));
            }
            else
            {
                projectTypes[p.Name] = p.Type;
            }
        }

        // ---- Validate modules ----
        foreach (var m in modules)
        {
            if (!ValidModuleNames.Contains(m.Name))
            {
                errors.Add(new("INVALID_MODULE",
                    $"Module '{m.Name}' is not a supported module. " +
                    $"Supported modules: {string.Join(", ", ValidModuleNames)}."));
                continue;
            }

            var allowedTypes = ModuleCompatibility[m.Name];

            foreach (var target in m.AppliesTo)
            {
                if (!projectTypes.TryGetValue(target, out var projectType))
                    continue; // target project type unknown (already reported above)

                // Projects that must have no modules
                if (NoModuleTypes.Contains(projectType))
                {
                    errors.Add(new("NO_MODULES_ALLOWED",
                        $"Module '{m.Name}' cannot be applied to project '{target}' (type '{projectType}'). " +
                        $"{projectType} must remain dependency-free — it communicates with other projects " +
                        "only through defined interfaces."));
                    continue;
                }

                // Module–type compatibility
                if (!allowedTypes.Contains(projectType))
                {
                    errors.Add(new("MODULE_INCOMPATIBLE",
                        $"Module '{m.Name}' cannot be applied to project '{target}' (type '{projectType}'). " +
                        $"'{m.Name}' is only valid for: {string.Join(", ", allowedTypes)}."));
                }
            }
        }

        return errors.Count == 0
            ? SolutionDslValidationResult.Valid()
            : SolutionDslValidationResult.Invalid(errors);
    }

    // -------------------------------------------------------------------------
    // Minimal YAML parser for the constrained Solution.dsl schema
    // -------------------------------------------------------------------------

    private sealed class SolutionProject { public string Name { get; set; } = ""; public string Type { get; set; } = ""; }
    private sealed class SolutionModule  { public string Name { get; set; } = ""; public List<string> AppliesTo { get; } = []; }

    private enum Section { None, Projects, Modules, Other }

    private static (List<SolutionProject> projects, List<SolutionModule> modules) ParseSolutionDsl(string yaml)
    {
        var projects = new List<SolutionProject>();
        var modules  = new List<SolutionModule>();

        var section        = Section.None;
        SolutionProject?   currentProject = null;
        SolutionModule?    currentModule  = null;
        var inAppliesTo = false;

        foreach (var rawLine in yaml.Split('\n'))
        {
            var line    = rawLine.TrimEnd();
            var trimmed = line.TrimStart();

            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

            var indent = line.Length - trimmed.Length;

            // ---- Top-level section keys ----
            if (indent == 0)
            {
                inAppliesTo = false;
                if (trimmed.StartsWith("projects:"))  { section = Section.Projects; currentProject = null; continue; }
                if (trimmed.StartsWith("modules:"))   { section = Section.Modules;  currentModule  = null; continue; }
                section = Section.Other;
                continue;
            }

            // ---- Projects section ----
            if (section == Section.Projects)
            {
                if (indent == 2 && trimmed.StartsWith("- name:"))
                {
                    currentProject = new SolutionProject { Name = ExtractInlineValue(trimmed, "- name:") };
                    projects.Add(currentProject);
                }
                else if (indent == 4 && currentProject != null && trimmed.StartsWith("type:"))
                {
                    currentProject.Type = ExtractInlineValue(trimmed, "type:");
                }
            }

            // ---- Modules section ----
            if (section == Section.Modules)
            {
                if (indent == 2 && trimmed.StartsWith("- name:"))
                {
                    inAppliesTo   = false;
                    currentModule = new SolutionModule { Name = ExtractInlineValue(trimmed, "- name:") };
                    modules.Add(currentModule);
                }
                else if (indent == 4 && currentModule != null && trimmed.StartsWith("applies_to:"))
                {
                    inAppliesTo = true;
                }
                else if (inAppliesTo && indent >= 6 && trimmed.StartsWith("- ") && currentModule != null)
                {
                    var target = trimmed[2..].Trim();
                    if (!string.IsNullOrEmpty(target))
                        currentModule.AppliesTo.Add(target);
                }
                else if (indent <= 4 && !trimmed.StartsWith("- ") && currentModule != null)
                {
                    inAppliesTo = false;
                }
            }
        }

        return (projects, modules);
    }

    private static string ExtractInlineValue(string line, string prefix)
    {
        var idx = line.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0) return string.Empty;
        var value = line[(idx + prefix.Length)..].Trim();
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1];
        }
        return value;
    }
}
