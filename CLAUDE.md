@AGENTS.md

## Claude Code

The Unity MCP server (`unity-editor-mcp`) is configured at **local** scope, so it is
private to this machine and this project — it lives in `~/.claude.json`, not in the repo.
Run `claude mcp get unity-editor-mcp` to check it, or see AGENTS.md to set it up again.

When the MCP tools are connected, use them instead of `unity command` in Bash; they carry
structured results. Fall back to the CLI when they aren't.
