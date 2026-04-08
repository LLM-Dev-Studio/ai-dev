using System;
using System.Collections.Generic;
using System.Text.Json;
using AiDev;
using AiDev.Features.Board;
using AiDev.Models.Types;

var taskId = TaskId.New();
var state = new Dictionary<string, object?>();
var board = new { columns = new [] { new BoardColumn(ColumnId.Backlog, "Backlog", new List<TaskId>{ taskId }), new BoardColumn(ColumnId.InProgress, "In Progress"), new BoardColumn(ColumnId.Review, "Review"), new BoardColumn(ColumnId.Done, "Done") }, tasks = new Dictionary<string, BoardTask> { [taskId.Value] = new BoardTask(taskId, "Investigate failure", description: "Check persistence") } };
var json = JsonSerializer.Serialize(board, JsonDefaults.WriteIgnoreNull);
Console.WriteLine(json);
var state2 = JsonSerializer.Deserialize<BoardStateProxy>(json, JsonDefaults.WriteIgnoreNull);
Console.WriteLine(state2?.Tasks?.Count ?? -1);
if (state2?.Tasks != null) foreach (var kv in state2.Tasks) Console.WriteLine($"{kv.Key} => {kv.Value.Title}");

public sealed class BoardStateProxy { public List<BoardColumn>? Columns { get; init; } public Dictionary<string, BoardTask>? Tasks { get; init; } }
