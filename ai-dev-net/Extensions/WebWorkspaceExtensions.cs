namespace AiDev.Ui.Web.Extensions;

public static class WebWorkspaceExtensions
{
    extension(IWebHostEnvironment env)
    {
        public RootDir RootDir()
        {
            var envVar = Environment.GetEnvironmentVariable("WORKSPACE_ROOT");
            if (string.IsNullOrEmpty(envVar))
                return new(Path.GetFullPath(
                    Path.Combine(env.ContentRootPath, "..", FilePathConstants.WorkspacesDirName)));

            // Reject relative paths and UNC paths (\\server\share) — must be an absolute local path.
            if (!Path.IsPathFullyQualified(envVar) || envVar.StartsWith(@"\\"))
                throw new InvalidOperationException(
                    $"WORKSPACE_ROOT must be an absolute local path, got: '{envVar}'");

            return new(envVar);
        }
    }
}
