namespace AiDev.Executors;

/// <summary>
/// Anthropic tool definitions for all workspace tools.
/// Injected into /v1/messages requests when the "mcp-workspace" skill is enabled.
/// Uses Anthropic's input_schema format (distinct from Ollama's parameters format).
/// Tool names must match the constants in WorkspaceTools.
/// </summary>
internal static class AnthropicToolSchemas
{
    // Raw JSON is clearest here — the schema structure is data, not logic.
    // Anthropic format: top-level name/description/input_schema (no "function" wrapper).
    public const string ToolsJson = """
    [
      {
        "name": "read_file",
        "description": "Read a file within a project. Path is relative to the project root (e.g. 'board/board.json', 'agents/dev-alex/inbox/msg.md'). Also accepts absolute paths within the target project.",
        "input_schema": {
          "type": "object",
          "properties": {
            "project_slug": {
              "type": "string",
              "description": "Project slug (e.g. 'demo-project')"
            },
            "path": {
              "type": "string",
              "description": "Relative or absolute path to the file"
            }
          },
          "required": ["project_slug", "path"]
        }
      },
      {
        "name": "list_directory",
        "description": "List files and subdirectories in a directory within a project. Path is relative to the project root.",
        "input_schema": {
          "type": "object",
          "properties": {
            "project_slug": {
              "type": "string",
              "description": "Project slug (e.g. 'demo-project')"
            },
            "path": {
              "type": "string",
              "description": "Relative path to the directory (e.g. 'agents/dev-alex/inbox', 'board')"
            }
          },
          "required": ["project_slug", "path"]
        }
      },
      {
        "name": "update_board",
        "description": "Atomically update the board state. Accepts the complete board JSON. The JSON is parsed and re-serialised to prevent malformed data. The board file is at board/board.json in the target project.",
        "input_schema": {
          "type": "object",
          "properties": {
            "project_slug": {
              "type": "string",
              "description": "Project slug (e.g. 'demo-project')"
            },
            "board_json": {
              "type": "string",
              "description": "Complete board JSON content (must be valid JSON with 'columns' and 'tasks' properties)"
            }
          },
          "required": ["project_slug", "board_json"]
        }
      },
      {
        "name": "update_agent_status",
        "description": "Update status fields of an agent's agent.json file. Only modifies status, sessionStartedAt, and pid — all other fields are preserved.",
        "input_schema": {
          "type": "object",
          "properties": {
            "project_slug": {
              "type": "string",
              "description": "Project slug (e.g. 'demo-project')"
            },
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
          "required": ["project_slug", "agent_slug", "status"]
        }
      },
      {
        "name": "write_journal",
        "description": "Append to or create a daily journal entry for an agent. Journal files live at agents/{slug}/journal/YYYY-MM-DD.md in the target project.",
        "input_schema": {
          "type": "object",
          "properties": {
            "project_slug": {
              "type": "string",
              "description": "Project slug (e.g. 'demo-project')"
            },
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
          "required": ["project_slug", "agent_slug", "date", "content"]
        }
      },
      {
        "name": "write_inbox",
        "description": "Write a message file to another agent's inbox directory. Validates that the target agent directory exists before writing.",
        "input_schema": {
          "type": "object",
          "properties": {
            "project_slug": {
              "type": "string",
              "description": "Project slug (e.g. 'demo-project')"
            },
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
          "required": ["project_slug", "agent_slug", "filename", "content"]
        }
      },
      {
        "name": "write_outbox",
        "description": "Write a copy of a sent message to the calling agent's outbox directory.",
        "input_schema": {
          "type": "object",
          "properties": {
            "project_slug": {
              "type": "string",
              "description": "Project slug (e.g. 'demo-project')"
            },
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
          "required": ["project_slug", "agent_slug", "filename", "content"]
        }
      },
      {
        "name": "write_decision",
        "description": "Write a decision request file to the decisions/pending/ directory. Used when an agent is blocked and needs a human to decide something.",
        "input_schema": {
          "type": "object",
          "properties": {
            "project_slug": {
              "type": "string",
              "description": "Project slug (e.g. 'demo-project')"
            },
            "filename": {
              "type": "string",
              "description": "Decision filename (e.g. '20260402-090000-auth-middleware.md')"
            },
            "content": {
              "type": "string",
              "description": "Full decision content including YAML frontmatter"
            }
          },
          "required": ["project_slug", "filename", "content"]
        }
      },
      {
        "name": "read_kb",
        "description": "Read a knowledge base article by slug. Articles are markdown files in the kb/ directory of the target project.",
        "input_schema": {
          "type": "object",
          "properties": {
            "project_slug": {
              "type": "string",
              "description": "Project slug (e.g. 'demo-project')"
            },
            "slug": {
              "type": "string",
              "description": "Article slug (filename without .md extension, e.g. 'agent-setup-guide')"
            }
          },
          "required": ["project_slug", "slug"]
        }
      }
    ]
    """;
}
