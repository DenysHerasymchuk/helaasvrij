import { type ReactNode, useState } from "react";
import { Check, Copy01 } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { Link } from "react-router";

const serverPath = "/path/to/internship-coach/mcp/dist/index.js";
const defaultApiUrl = "http://localhost:5080";

// The server reads its tool list from GET /api/tools (backend ChatToolbox); keep these groups in sync with it.
const tools: { names: string[]; does: string; writes: boolean }[] = [
    { names: ["list_postings", "get_posting", "list_applications", "search_jobs"], does: "Look at your positions, one position in full, the board, and open Belgian job ads.", writes: false },
    { names: ["get_active_sprint", "list_sprints", "get_sprint_report", "list_backlog", "get_position_activity"], does: "The running sprint with its positions by stage, past sprints and their reports, the backlog, and the history of one position.", writes: false },
    { names: ["get_activity", "get_calendar", "list_gaps", "get_journey", "get_profile", "list_github_projects", "list_documents"], does: "Latest moves, interviews and deadlines in the next 14 days, your gap plan, and what your profile and documents hold.", writes: false },
    { names: ["set_application_stage", "add_to_sprint", "remove_from_sprint"], does: "Move a position to another stage, or in and out of the running sprint.", writes: true },
    { names: ["start_sprint", "complete_sprint"], does: "Start a sprint, or complete the running one (open positions return to the backlog).", writes: true },
    { names: ["add_position", "save_job", "add_to_applications"], does: "Add a position from text or a link, save a job from search, or put a saved position on the board. Uses one AI call.", writes: true },
    { names: ["set_notes", "tick_gap", "untick_gap"], does: "Replace the notes on a card, tick a gap (re-reads the repo and uses AI) or undo it.", writes: true },
];

const prompts = ["Do my weekly review", "I got an interview at Barco, move it and help me prepare", "Add the Brightlane position to my sprint"];

function Section({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
    return (
        <section className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
            <header className="border-b border-secondary px-5 py-4">
                <h2 className="text-md font-semibold text-primary">{title}</h2>
                {description && <p className="mt-0.5 text-sm text-tertiary">{description}</p>}
            </header>
            <div className="px-5 py-5">{children}</div>
        </section>
    );
}

function CopyButton({ text, label }: { text: string; label: string }) {
    const [state, setState] = useState<"idle" | "copied" | "failed">("idle");
    const copy = async () => {
        try {
            await navigator.clipboard.writeText(text);
            setState("copied");
        } catch {
            setState("failed");
        }
        window.setTimeout(() => setState("idle"), 2000);
    };
    return (
        <>
            <Button size="sm" color="secondary" iconLeading={state === "copied" ? Check : Copy01} onPress={copy} aria-label={`Copy ${label}`}>
                {state === "copied" ? "Copied" : state === "failed" ? "Select and copy" : "Copy"}
            </Button>
            <span role="status" className="sr-only">
                {state === "copied" ? `${label} copied` : state === "failed" ? "Copy failed, select the text and copy it yourself" : ""}
            </span>
        </>
    );
}

function CodeBlock({ code, label }: { code: string; label: string }) {
    return (
        <div className="overflow-hidden rounded-lg ring-1 ring-secondary">
            <div className="flex items-center justify-between gap-3 border-b border-secondary bg-secondary px-3 py-2">
                <span className="text-xs font-medium text-tertiary">{label}</span>
                <CopyButton text={code} label={label} />
            </div>
            <pre
                tabIndex={0}
                aria-label={label}
                className="overflow-x-auto bg-primary p-3 font-mono text-xs leading-5 text-secondary outline-focus-ring focus-visible:outline-2 focus-visible:-outline-offset-2"
            >
                <code>{code}</code>
            </pre>
        </div>
    );
}

function Step({ n, title, children }: { n: number; title: string; children: ReactNode }) {
    return (
        <li className="flex gap-4">
            <span aria-hidden className="flex size-7 shrink-0 items-center justify-center rounded-full bg-secondary text-sm font-semibold text-secondary ring-1 ring-secondary">
                {n}
            </span>
            <div className="flex min-w-0 flex-1 flex-col gap-3">
                <h3 className="pt-0.5 text-sm font-semibold text-primary">{title}</h3>
                {children}
            </div>
        </li>
    );
}

const Mono = ({ children }: { children: ReactNode }) => <code className="rounded bg-secondary px-1 py-0.5 font-mono text-xs text-primary">{children}</code>;

export function ConnectClaudePage() {

    const claudeCode = `claude mcp add helaasvrij \\
  -e COACH_TOKEN='hv_...' \\
  -- node ${serverPath}`;

    const claudeDesktop = JSON.stringify(
        {
            mcpServers: {
                helaasvrij: {
                    command: "node",
                    args: [serverPath],
                    env: { COACH_API_URL: defaultApiUrl, COACH_TOKEN: "hv_..." },
                },
            },
        },
        null,
        2,
    );

    return (
        <div className="mx-auto flex max-w-3xl flex-col gap-6 px-4 py-8 md:px-8">
            <div>
                <h1 className="text-display-xs font-semibold text-primary">Connect Claude</h1>
                <p className="mt-1 text-md text-tertiary">
                    Use helaasvrij from Claude Code or Claude Desktop: ask Claude to review your sprint, add a position, move a card or list your gaps. The
                    connector runs on your own computer and acts as you through a token you can revoke, so it only sees your data. Changes are proposed first and only happen after you agree.
                </p>
            </div>

            <Section title="Set it up" description="Takes about two minutes. You need Node 22 or newer and the app's API running.">
                <ol className="flex flex-col gap-6">
                    <Step n={1} title="Build the connector">
                        <p className="text-sm text-tertiary">Run this in the internship-coach repository.</p>
                        <CodeBlock label="Build command" code="cd mcp && npm install && npm run build" />
                    </Step>
                    <Step n={2} title="Make an access token">
                        <p className="text-sm text-tertiary">
                            Open{" "}
                            <Link to="/settings/account" className="font-semibold text-brand-secondary hover:underline">
                                Settings, Account
                            </Link>{" "}
                            and create a token. Copy it right away: it starts with <Mono>hv_</Mono> and is shown once. It works for GitHub accounts too.
                        </p>
                    </Step>
                    <Step n={3} title="Add it to Claude">
                        <p className="text-sm text-tertiary">
                            Replace <Mono>{serverPath}</Mono> with the full path on your computer, and <Mono>hv_...</Mono> with your token. Pick the app you
                            use.
                        </p>
                        <h4 className="text-sm font-medium text-secondary">Claude Code</h4>
                        <CodeBlock label="Claude Code command" code={claudeCode} />
                        <h4 className="text-sm font-medium text-secondary">Claude Desktop</h4>
                        <p className="text-sm text-tertiary">
                            Paste this into <Mono>claude_desktop_config.json</Mono>. If the file already has <Mono>mcpServers</Mono>, add only the{" "}
                            <Mono>helaasvrij</Mono> entry.
                        </p>
                        <CodeBlock label="Claude Desktop config" code={claudeDesktop} />
                    </Step>
                    <Step n={4} title="Check the API address">
                        <p className="text-sm text-tertiary">
                            <Mono>COACH_API_URL</Mono> defaults to <Mono>{defaultApiUrl}</Mono>, the API from <Mono>docker compose up</Mono>. Running it
                            somewhere else? For Claude Code add <Mono>-e COACH_API_URL=https://...</Mono>; for Claude Desktop change the value in the JSON.
                        </p>
                    </Step>
                    <Step n={5} title="Restart Claude and ask something">
                        <p className="text-sm text-tertiary">Restart Claude Desktop, or start a new Claude Code session. Then try one of the prompts below.</p>
                    </Step>
                </ol>
            </Section>

            <Section title="Things to ask" description="Paste one into Claude.">
                <ul className="flex flex-col gap-3">
                    {prompts.map((p) => (
                        <li key={p} className="flex items-center justify-between gap-3 rounded-lg px-3 py-2 ring-1 ring-secondary">
                            <span className="min-w-0 text-sm text-primary">{p}</span>
                            <CopyButton text={p} label={`prompt "${p}"`} />
                        </li>
                    ))}
                </ul>
            </Section>

            <Section title="What Claude can do" description="Read tools only look. Tools that change something only propose the change; Claude asks you, then confirms.">
                <div className="flex flex-col gap-3">
                    {tools.map((t) => (
                        <div key={t.names[0]} className="flex flex-col gap-1.5 rounded-lg px-3 py-3 ring-1 ring-secondary sm:flex-row sm:gap-4">
                            <div className="flex shrink-0 flex-wrap items-start gap-1.5 sm:w-64">
                                {t.names.map((n) => (
                                    <Mono key={n}>{n}</Mono>
                                ))}
                                <Badge type="pill-color" size="sm" color={t.writes ? "warning" : "gray"}>
                                    {t.writes ? "Changes data" : "Read only"}
                                </Badge>
                            </div>
                            <div className="flex min-w-0 flex-col gap-1">
                                <p className="text-sm text-primary">{t.does}</p>
                            </div>
                        </div>
                    ))}
                </div>
            </Section>

            <Section title="Safety">
                <ul className="flex list-disc flex-col gap-2 pl-5 text-sm text-tertiary">
                    <li>Claude can't send messages to employers, delete anything or change your settings. There are no tools for that.</li>
                    <li>Tools that change something only propose it. Nothing happens until Claude calls the confirm step after you agreed.</li>
                    <li>
                        Your token sits in plain text in your local Claude config. The connector sends it only to <Mono>COACH_API_URL</Mono> and never logs it.
                        It can't sign in to the app or make tokens, and you can revoke it in Settings, Account. Don't commit or share that file.
                    </li>
                    <li>Claude sees what the tools return: your positions, board, sprints and gap plan. It doesn't get your CV text.</li>
                    <li>Demoing on a shared screen? Use a test account, not your real one.</li>
                </ul>
            </Section>

            <Section title="If it doesn't work">
                <dl className="flex flex-col gap-4 text-sm">
                    <div>
                        <dt className="font-medium text-primary">Claude says the request failed or can't connect</dt>
                        <dd className="mt-1 text-tertiary">
                            The API isn't running or <Mono>COACH_API_URL</Mono> is wrong. Start it with <Mono>docker compose up</Mono> and check that{" "}
                            <Mono>{defaultApiUrl}</Mono> answers.
                        </dd>
                    </div>
                    <div>
                        <dt className="font-medium text-primary">"The token is unknown or revoked"</dt>
                        <dd className="mt-1 text-tertiary">
                            Make a new token in Settings, Account, put it in <Mono>COACH_TOKEN</Mono>, then restart Claude.
                        </dd>
                    </div>
                    <div>
                        <dt className="font-medium text-primary">"Set COACH_TOKEN"</dt>
                        <dd className="mt-1 text-tertiary">The token is missing from the config.</dd>
                    </div>
                    <div>
                        <dt className="font-medium text-primary">The build fails or the server won't start</dt>
                        <dd className="mt-1 text-tertiary">
                            You need Node 22 or newer. Check with <Mono>node --version</Mono>.
                        </dd>
                    </div>
                </dl>
            </Section>
        </div>
    );
}
