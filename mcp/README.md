# helaasvrij MCP server

The app has a setup page for this at `/connect-claude` (account menu: Connect Claude).

A local stdio MCP server. It lets Claude (Claude Code or Claude Desktop) work your search as you: see the sprint and board, add positions, move cards, tick gaps. It calls the normal API with a personal access token, so it sees only your own data.

The tool list is not in this repo. The server reads it at start from `GET /api/tools`, the same catalog the in-app chat assistant uses, so a tool added in the backend shows up here after a restart.

## Tools

Read tools only look. Action tools only propose: the result tells Claude what would change and to ask you, and nothing happens until Claude calls `confirm_action` with the exact proposal (the same confirm step as the chat's Confirm button). `confirm_action` is marked destructive, so clients that ask before such tools will ask you.

| Kind | Tools |
| --- | --- |
| Read | `get_profile`, `list_github_projects`, `list_postings`, `get_posting`, `list_gaps`, `list_applications`, `get_journey`, `search_jobs`, `get_active_sprint`, `list_sprints`, `get_sprint_report`, `list_backlog`, `get_activity`, `get_calendar`, `list_documents`, `get_position_activity` |
| Action (propose, then confirm) | `set_application_stage`, `save_job`, `add_to_applications`, `add_to_sprint`, `remove_from_sprint`, `start_sprint`, `complete_sprint`, `add_position`, `set_notes`, `tick_gap`, `untick_gap` |
| Confirm | `confirm_action {tool, args}` |

Also: resources `helaasvrij://sprint/active`, `helaasvrij://backlog`, `helaasvrij://position/{postingId}`, and prompts `weekly_review` and `prepare_interview {position}`.

Results that contain postings, notes or ads are wrapped in `<untrusted_data>` tags so the model treats them as data, not instructions.

There are no tools that message employers, delete data or change settings.

## Build and run

Needs Node 22+ and a running API (default `http://localhost:5080`).

```sh
cd mcp
npm install
npm run build
npm test
```

Config (environment variables):

- `COACH_API_URL`: API base URL, default `http://localhost:5080`
- `COACH_TOKEN`: a personal access token. Create one in the app under Settings > Account > Access tokens. It starts with `hv_` and is shown once.

If the API is down or the token is wrong, the server stops at start and prints why on stderr.

## Claude Code

```sh
claude mcp add helaasvrij \
  -e COACH_TOKEN='hv_...' \
  -- node /path/to/internship-coach/mcp/dist/index.js
```

Add `-e COACH_API_URL=https://...` for a non-local API.

## Claude Desktop

In `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "helaasvrij": {
      "command": "node",
      "args": ["/path/to/internship-coach/mcp/dist/index.js"],
      "env": {
        "COACH_API_URL": "http://localhost:5080",
        "COACH_TOKEN": "hv_..."
      }
    }
  }
}
```

## Troubleshooting

- `could not load the tool list` at start: the API is not running or `COACH_API_URL` is wrong.
- `the token is unknown or revoked`: create a new token in Settings > Account and update the config. Tokens work for GitHub-only accounts too.
- Build or start fails: you need Node 22 or newer.

Use a test account for demos.

## Credentials stay local

The token sits in plain text in your local Claude config file. It cannot sign in to the app, create or revoke tokens, or change your password, and you can revoke it any time in Settings > Account. The server sends it only to `COACH_API_URL` and never logs it or your CV text. Do not commit or share that config file. Tool results go to the model you are chatting with, so Claude sees your positions, board and gap plan (not CV text or evidence quotes).
