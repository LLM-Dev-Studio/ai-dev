namespace AiDevNet.Features.Board;

public class BoardColumn
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<TaskId> TaskIds { get; set; } = [];
}
