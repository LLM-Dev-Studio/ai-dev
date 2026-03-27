namespace AiDevNet.Services;

public class ProjectsService(HttpClient http)
{
    public static Task<List<ProjectModel>> ListProjectsAsync() => Task.FromResult(new List<ProjectModel>());

    public async Task<List<ProjectModel>> GetProjectsAsync()
    {
        var result = await http.GetFromJsonAsync<List<ProjectModel>>("/api/projects");
        return result ?? new List<ProjectModel>();
    }
}

public class ProjectModel
{
    public string Slug { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public List<AgentModel> Agents { get; set; } = new();
}

public class AgentModel
{
    public string Status { get; set; }
    public int InboxCount { get; set; }
}
