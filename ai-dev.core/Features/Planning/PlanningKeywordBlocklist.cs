namespace AiDev.Features.Planning;

/// <summary>
/// EC-6: Phase 1 post-generation filter.
/// Scans assistant responses for technology keywords that violate the implementation-detail
/// prohibition defined in AD-02. Matching is case-insensitive with word-boundary detection
/// to avoid false positives on common substrings (e.g. "state" inside "estate").
/// </summary>
public static class PlanningKeywordBlocklist
{
    // AD-02 authoritative blocklist. See planning-tasks-screen-arch-decisions.md §AD-02.
    private static readonly string[] Keywords =
    [
        // Programming languages
        "Python", "Java", "Go", "Rust", "C#", "C\\+\\+", "JavaScript", "TypeScript",
        "Ruby", "PHP", "Swift", "Kotlin", "Scala", "Clojure", "Haskell", "Elixir",
        "Perl", "MATLAB",

        // Frontend frameworks
        "React", "Angular", "Vue", "Svelte", "Next\\.js", "Remix", "SvelteKit",
        "Astro", "Qwik", "Ember", "Backbone",

        // Backend frameworks
        "ASP\\.NET", "Django", "Flask", "FastAPI", "Spring Boot", "Spring",
        "Express", "Koa", "Fastify", "Sinatra", "Rails", "Laravel", "Symfony",
        "Zend", "Yii", "CakePHP", "Nest\\.js", "Deno", "Hono",

        // Mobile frameworks
        "React Native", "Flutter", "Xamarin", "Ionic", "NativeScript",
        "Kotlin Multiplatform", "Jetpack Compose",

        // Static site generators
        "Hugo", "Jekyll", "Gatsby", "Eleventy", "Pelican", "MkDocs",

        // Cloud platforms
        "AWS", "Azure", "GCP", "Google Cloud", "DigitalOcean", "Heroku", "Railway",
        "Vercel", "Netlify", "Fly\\.io", "Cloudflare",

        // AWS services
        "EC2", "Lambda", "S3", "CloudFront", "DynamoDB", "RDS", "Cognito",
        "API Gateway", "Kinesis", "SQS", "SNS", "AppSync", "Amplify",
        "CloudFormation", "Elastic Beanstalk", "AppRunner", "Lightsail",
        "Fargate", "ECS", "EKS", "SageMaker", "Bedrock",

        // Azure services
        "App Service", "Azure Functions", "Cosmos DB", "Service Bus", "Event Hubs",
        "Application Insights", "Azure DevOps", "Azure Monitor", "Key Vault",
        "Managed Identity", "Static Web Apps", "Logic Apps", "API Management",
        "Azure Container Instances", "Azure Kubernetes Service", "AKS",

        // GCP services
        "Compute Engine", "App Engine", "Cloud Run", "Cloud Functions",
        "Cloud Firestore", "BigQuery", "Pub/Sub", "Cloud Storage",

        // Databases
        "PostgreSQL", "MySQL", "MariaDB", "Oracle", "SQL Server", "SQLite",
        "MongoDB", "Cassandra", "CouchDB", "Firebase", "Supabase", "Redis",
        "Memcached", "Elasticsearch", "Solr", "Neo4j", "ArangoDB", "TiDB",
        "CockroachDB",

        // ORMs / data access
        "Entity Framework Core", "Entity Framework", "NHibernate", "Dapper",
        "PetaPoco", "SQLAlchemy", "Hibernate", "JPA", "Sequelize", "TypeORM",
        "Prisma", "Drizzle", "ActiveRecord", "Ecto", "Diesel",

        // Data warehousing
        "Snowflake", "Redshift", "Databricks", "Azure Synapse", "Dremio",

        // Message queues / event systems
        "RabbitMQ", "Apache Kafka", "Amazon Kinesis", "Apache Pulsar",
        "ActiveMQ", "ZeroMQ", "NATS", "Dapr",

        // API protocols / patterns
        "REST", "RESTful", "GraphQL", "SOAP", "gRPC", "WebSocket",
        "Server-Sent Events", "MQTT", "CoAP", "Protocol Buffers", "MessagePack",
        "Apache Avro", "OpenAPI", "Swagger", "AsyncAPI", "JSON-RPC", "XML-RPC",

        // Design patterns / architectural styles
        "CQRS", "Event Sourcing", "Microservices", "Serverless", "FaaS",
        "Saga Pattern", "Strangler Fig", "Bulkhead", "Circuit Breaker",
        "Event Streaming", "Lambda Architecture", "Kappa Architecture",
        "Backend for Frontend", "BFF", "Hexagonal Architecture", "Clean Architecture",
        "Layered Architecture", "Pipe-and-Filter", "Publish-Subscribe",
        "Eventual Consistency", "ACID", "BASE", "CAP Theorem",
        "Event-Driven Architecture", "Choreography", "Orchestration", "Repository Pattern", 

        // Auth & security
        "OAuth", "OpenID Connect", "JWT", "SAML", "Kerberos", "LDAP",
        "Active Directory", "Auth0", "Okta", "Azure AD", "AWS Cognito",
        "Firebase Auth", "Keycloak", "Identity Server",

        // Logging & monitoring
        "Serilog", "log4j", "NLog", "Bunyan", "Winston", "Pino",
        "Datadog", "New Relic", "Splunk", "ELK Stack", "Prometheus", "Grafana",
        "Jaeger", "Zipkin", "Dynatrace", "AppDynamics", "CloudWatch",

        // Testing
        "xUnit", "NUnit", "MSTest", "Selenium", "Playwright", "Cypress",
        "Jest", "Mocha", "Jasmine", "Vitest", "RSpec", "Cucumber", "Behave",
        "TestNG", "Mockito", "NSubstitute", "Moq", "sinon",

        // CI/CD
        "Jenkins", "GitLab CI", "GitHub Actions", "CircleCI", "Travis CI",
        "Azure Pipelines", "TeamCity", "GoCD", "Bamboo", "DroneCI",

        // Containerisation
        "Docker", "Podman", "Buildah", "Containerd",

        // Orchestration
        "Kubernetes", "K8s", "Docker Swarm", "Nomad", "Mesos", "OpenShift",

        // Package management
        "npm", "yarn", "pnpm", "NuGet", "Maven", "Gradle", "pip", "poetry",
        "Cargo", "Composer", "Bundler", "Go Modules",

        // Build tools
        "webpack", "Vite", "Rollup", "Parcel", "Gulp", "Grunt", "Make",
        "Bazel", "Ant", "MSBuild", "Cake", "Paket", "dotnet CLI", "dotnet",
        "\\.NET",

        // IaC / DevOps
        "Terraform", "CloudFormation", "Ansible", "Puppet", "Chef",
        "SaltStack", "Helm", "Kustomize", "Flux", "ArgoCD", "Skaffold",

        // Observability
        "OpenTelemetry", "OpenTracing", "OpenMetrics", "Micrometer",

        // Version control
        "GitHub", "GitLab", "Bitbucket", "Gitea", "Gerrit", "Perforce",
        "Mercurial", "Subversion", "SVN", "TFS",
    ];

    // Pre-compiled regex: word-boundary match on each keyword, case-insensitive.
    // Uses \b where applicable; for keywords containing non-word chars we use lookahead/lookbehind.
    private static readonly Regex BlocklistRegex = BuildRegex();

    private static Regex BuildRegex()
    {
        // Wrap each keyword in word-boundary assertions.
        // For terms containing special regex chars (already escaped in Keywords list),
        // we still wrap in \b boundaries so partial matches don't fire.
        var patterns = Keywords.Select(k => $@"(?<![a-zA-Z0-9_\-]){k}(?![a-zA-Z0-9_\-])");
        var combined = string.Join("|", patterns);
        return new Regex(combined, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Returns the first matching keyword found in <paramref name="text"/>, or null if none.
    /// </summary>
    public static string? FindFirstMatch(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = BlocklistRegex.Match(text);
        return m.Success ? m.Value : null;
    }

    /// <summary>
    /// Returns true if <paramref name="text"/> contains any blocked keyword.
    /// </summary>
    public static bool ContainsBlockedKeyword(string text) => FindFirstMatch(text) != null;

    /// <summary>
    /// Substitute message shown to the user when EC-6 filtering is triggered.
    /// </summary>
    public const string FilterSubstituteMessage =
        "I noticed that response contained technical implementation details, which aren't appropriate " +
        "for this phase. Could you rephrase your requirement in business terms? For example, instead of " +
        "naming a specific technology, describe what the system needs to do for its users.";
}
