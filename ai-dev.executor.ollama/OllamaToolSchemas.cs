using System.Text.Json.Nodes;

namespace AiDev.Executors;

/// <summary>
/// Ollama function-calling schemas for all workspace tools.
/// Injected into /api/chat requests when the "mcp-workspace" skill is enabled.
/// Tool names must match the constants in <see cref="WorkspaceTools"/>.
/// </summary>
internal static class OllamaToolSchemas
{
    // Raw JSON is clearest here — the schema structure is data, not logic.
    private static readonly string ToolsJson = """
    [
      {
        "type": "function",
        "function": {
          "name": "read_file",
          "description": "Read a file within the workspace. Path is relative to the workspace root (e.g. 'board/board.json', 'agents/dev-alex/inbox/msg.md'). Also accepts absolute paths within the workspace.",
          "parameters": {
            "type": "object",
            "properties": {
              "path": {
                "type": "string",
                "description": "Relative or absolute path to the file"
              }
            },
            "required": ["path"]
          }
        }
      },
      {
        "type": "function",
        "function": {
          "name": "list_directory",
          "description": "List files and subdirectories in a directory within the workspace. Path is relative to the workspace root.",
          "parameters": {
            "type": "object",
            "properties": {
              "path": {
                "type": "string",
                "description": "Relative path to the directory (e.g. 'agents/dev-alex/inbox', 'board')"
              }
            },
            "required": ["path"]
          }
        }
      },
      {
        "type": "function",
        "function": {
          "name": "update_board",
          "description": "Atomically update the board state. Accepts the complete board JSON. The JSON is parsed and re-serialised to prevent malformed data. The board file is at board/board.json in the workspace.",
          "parameters": {
            "type": "object",
            "properties": {
              "board_json": {
                "type": "string",
                "description": "Complete board JSON content (must be valid JSON with 'columns' and 'tasks' properties)"
              }
            },
            "required": ["board_json"]
          }
        }
      },
      {
        "type": "function",
        "function": {
          "name": "update_agent_status",
          "description": "Update status fields of an agent's agent.json file. Only modifies status, sessionStartedAt, and pid — all other fields are preserved.",
          "parameters": {
            "type": "object",
            "properties": {
              "agent_slug": {
                "type": "string",
                "description": "Agent slug (e.g. 'dev-alex', 'pm-morgan')"
              },
              "status": {
                "type": "string",
                "description": "New status: 'idle', 'running', or 'error'"
              },
              "session_started_at": {
                "type": "string",
                "description": "ISO 8601 UTC timestamp for session start, or empty/omit to clear"
              },
              "pid": {
                "type": "integer",
                "description": "Process ID, or omit to clear"
              }
            },
            "required": ["agent_slug", "status"]
          }
        }
      },
      {
        "type": "function",
        "function": {
          "name": "write_journal",
          "description": "Append to or create a daily journal entry for an agent. Journal files live at agents/{slug}/journal/YYYY-MM-DD.md.",
          "parameters": {
            "type": "object",
            "properties": {
              "agent_slug": {
                "type": "string",
                "description": "Agent slug"
              },
              "date": {
                "type": "string",
                "description": "Date in YYYY-MM-DD format (e.g. '2026-04-06')"
              },
              "content": {
                "type": "string",
                "description": "Markdown content to append to the journal"
              }
            },
            "required": ["agent_slug", "date", "content"]
          }
        }
      },
      {
        "type": "function",
        "function": {
          "name": "write_inbox",
          "description": "Write a message file to another agent's inbox directory. Validates that the target agent directory exists before writing.",
          "parameters": {
            "type": "object",
            "properties": {
              "agent_slug": {
                "type": "string",
                "description": "Slug of the target agent (e.g. 'dev-alex')"
              },
              "filename": {
                "type": "string",
                "description": "Message filename (e.g. '20260402-090000-from-pm-morgan.md')"
              },
              "content": {
                "type": "string",
                "description": "Full message content including YAML frontmatter"
              }
            },
            "required": ["agent_slug", "filename", "content"]
          }
        }
      },
      {
        "type": "function",
        "function": {
          "name": "write_outbox",
          "description": "Write a copy of a sent message to the calling agent's outbox directory.",
          "parameters": {
            "type": "object",
            "properties": {
              "agent_slug": {
                "type": "string",
                "description": "Slug of the sending agent (your own slug)"
              },
              "filename": {
                "type": "string",
                "description": "Message filename"
              },
              "content": {
                "type": "string",
                "description": "Full message content including YAML frontmatter"
              }
            },
            "required": ["agent_slug", "filename", "content"]
          }
        }
      },
      {
        "type": "function",
        "function": {
          "name": "write_decision",
          "description": "Write a decision request file to the decisions/pending/ directory. Used when an agent is blocked and needs a human to decide something.",
          "parameters": {
            "type": "object",
            "properties": {
              "filename": {
                "type": "string",
                "description": "Decision filename (e.g. '20260402-090000-auth-middleware.md')"
              },
              "content": {
                "type": "string",
                "description": "Full decision content including YAML frontmatter"
              }
            },
            "required": ["filename", "content"]
          }
        }
      },
      {
        "type": "function",
        "function": {
          "name": "read_kb",
          "description": "Read a knowledge base article by slug. Articles are markdown files in the kb/ directory.",
          "parameters": {
            "type": "object",
            "properties": {
              "slug": {
                "type": "string",
                "description": "Article slug (filename without .md extension, e.g. 'agent-setup-guide')"
              }
            },
            "required": ["slug"]
          }
        }
      }
    ]
    """;

    /// <summary>Returns a fresh JsonArray of tool definitions for inclusion in an Ollama /api/chat request.</summary>
    public static JsonArray GetToolsArray() => JsonNode.Parse(ToolsJson)!.AsArray();
}
