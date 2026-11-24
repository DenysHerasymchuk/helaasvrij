# Internship Coach MCP server

The app has a setup page for this at `/connect-claude` (sidebar: Connect Claude) with your email prefilled.

A local stdio MCP server. It lets Claude (Claude Code or Claude Desktop) use Internship Coach as you, the signed-in student. It calls the normal API with your session cookie, so it sees only your own data.

## Tools

| Tool | What it does | Changes data |
| --- | --- | --- |
| `list_postings` | Saved postings with match score and board stage | no |
| `get_posting {postingId}` | One posting with requirements, coverage, score and gap actions | no |
| `search_jobs {what?, where?}` | Search the Adzuna snapshot; returns `jobId`s | no |
| `save_job {jobId}` | Save an Adzuna job to your postings (one AI call) | yes |
| `add_posting {text?, url?}` | Add a posting from pasted text or a URL (one AI call) | yes |
| `list_applications` | The applications board | no |
| `set_application_stage {postingId? \| applicationId? \| query?, stage, notes?}` | Move a card to Saved, Applied, Interview, Offer or Rejected. Puts the posting on the board first if needed. An ambiguous `query` changes nothing and returns the candidates. | yes |
| `list_gaps` | Open gaps across all postings, with actions | no |

There are no tools that message employers or delete data.

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
- `COACH_EMAIL`, `COACH_PASSWORD`: your Internship Coach login (email and password account)

The server logs in on the first call, keeps the cookie in memory only, and logs in again once if the session expires.

## Claude Code

```sh
claude mcp add helaasvrij \
  -e COACH_EMAIL=you@example.com -e COACH_PASSWORD='your-password' \
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
        "COACH_EMAIL": "you@example.com",
        "COACH_PASSWORD": "your-password"
      }
    }
  }
}
```

## Troubleshooting

- Request fails or cannot connect: the API is not running or `COACH_API_URL` is wrong.
- `Login failed: 401`: wrong `COACH_EMAIL` or `COACH_PASSWORD`. An account you only use through GitHub has no password, so it cannot log in.
- Build or start fails: you need Node 22 or newer.

Use a test account for demos.

## Credentials stay local

Your password is stored in plain text in your local Claude config file and passed to this process only. The server sends it only to `COACH_API_URL` and never logs it, the cookie or your CV text. Do not commit or share that config file. Tool results go to the model you are chatting with, so Claude sees your postings, board and gap plan (not CV text or evidence quotes).
