namespace AiDevNet.Features.Board;

public class BoardData
{
    public List<BoardColumn> Columns { get; set; } = [];
    public Dictionary<TaskId, BoardTask> Tasks { get; set; } = new();
}
