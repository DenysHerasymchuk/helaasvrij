import { type ReactNode, useState } from "react";
import { Check, Copy01 } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { useAuth } from "@/core/auth";
import { stages } from "@/features/applications/types";

const serverPath = "/path/to/internship-coach/mcp/dist/index.js";
const defaultApiUrl = "http://localhost:5080";

// Mirrors the tools registered in mcp/src/index.ts; keep both in sync.
const tools: { name: string; does: string; inputs: string; writes: boolean }[] = [
    { name: "list_postings", does: "Lists your saved postings with match score and board stage.", inputs: "none", writes: false },
    { name: "get_posting", does: "Shows one posting with its requirements, coverage, score and gap actions.", inputs: "postingId", writes: false },
    { name: "search_jobs", does: "Searches the Belgian internship ads. Returns a jobId per result.", inputs: "what (optional), where (optional)", writes: false },
    { name: "save_job", does: "Saves a job from search_jobs to your postings. Uses one AI call.", inputs: "jobId", writes: true },
    { name: "add_posting", does: "Adds a posting from pasted text or a URL. Uses one AI call.", inputs: "text or url", writes: true },
    { name: "list_applications", does: "Lists the cards on your applications board.", inputs: "none", writes: false },
    {
        name: "set_application_stage",
        does: "Moves a card to another stage and puts the posting on the board first if needed. If the query matches more than one posting, nothing changes and you get the options.",
        inputs: "postingId, applicationId or query; stage; notes (optional, replaces the card's notes)",
        writes: true,
    },
    { name: "list_gaps", does: "Lists your open gaps across all postings, highest impact first, with the action for each.", inputs: "none", writes: false },
];

const prompts = ["I got an interview at Barco", "Find internships in Gent with React", "What should I fix first for Brightlane?"];

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
    const { user } = useAuth();
    const email = user?.email ?? "you@example.com";

    const claudeCode = `claude mcp add helaasvrij \\
  -e COACH_EMAIL=${email} -e COACH_PASSWORD='your-password' \\
  -- node ${serverPath}`;

    const claudeDesktop = JSON.stringify(
        {
            mcpServers: {
                helaasvrij: {
                    command: "node",
                    args: [serverPath],
                    env: { COACH_API_URL: defaultApiUrl, COACH_EMAIL: email, COACH_PASSWORD: "your-password" },
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
                    Use helaasvrij from Claude Code or Claude Desktop: ask Claude to move a posting to Interview, find internships, or list your gaps. The
                    connector runs on your own computer and acts as you, so it only sees your data. Your password stays in your own Claude config.
                </p>
            </div>

            <Section title="Set it up" description="Takes about two minutes. You need Node 22 or newer and the app's API running.">
                <ol className="flex flex-col gap-6">
                    <Step n={1} title="Build the connector">
                        <p className="text-sm text-tertiary">Run this in the internship-coach repository.</p>
                        <CodeBlock label="Build command" code="cd mcp && npm install && npm run build" />
                    </Step>
                    <Step n={2} title="Add it to Claude">
                        <p className="text-sm text-tertiary">
                            Replace <Mono>{serverPath}</Mono> with the full path on your computer, and <Mono>your-password</Mono> with your helaasvrij
                            password. Pick the app you use.
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
                    <Step n={3} title="Check the API address">
                        <p className="text-sm text-tertiary">
                            <Mono>COACH_API_URL</Mono> defaults to <Mono>{defaultApiUrl}</Mono>, the API from <Mono>docker compose up</Mono>. Running it
                            somewhere else? For Claude Code add <Mono>-e COACH_API_URL=https://...</Mono>; for Claude Desktop change the value in the JSON.
                        </p>
                    </Step>
                    <Step n={4} title="Restart Claude and ask something">
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

            <Section title="What Claude can do" description="Eight tools. Read tools only look; write tools change your postings or board.">
                <div className="flex flex-col gap-3">
                    {tools.map((t) => (
                        <div key={t.name} className="flex flex-col gap-1.5 rounded-lg px-3 py-3 ring-1 ring-secondary sm:flex-row sm:gap-4">
                            <div className="flex shrink-0 items-start gap-2 sm:w-56 sm:flex-col">
                                <Mono>{t.name}</Mono>
                                <Badge type="pill-color" size="sm" color={t.writes ? "warning" : "gray"}>
                                    {t.writes ? "Changes data" : "Read only"}
                                </Badge>
                            </div>
                            <div className="flex min-w-0 flex-col gap-1">
                                <p className="text-sm text-primary">{t.does}</p>
                                <p className="text-xs text-tertiary">Inputs: {t.inputs}</p>
                                {t.name === "set_application_stage" && (
                                    <div className="mt-1 flex flex-wrap items-center gap-1.5">
                                        <span className="text-xs text-tertiary">Stages:</span>
                                        {stages.map((s) => (
                                            <Badge key={s.id} type="pill-color" size="sm" color={s.color}>
                                                {s.label}
                                            </Badge>
                                        ))}
                                    </div>
                                )}
                            </div>
                        </div>
                    ))}
                </div>
            </Section>

            <Section title="Safety">
                <ul className="flex list-disc flex-col gap-2 pl-5 text-sm text-tertiary">
                    <li>Claude can't send messages to employers and can't delete anything. There are no tools for that.</li>
                    <li>
                        Your password sits in plain text in your local Claude config. The connector sends it only to <Mono>COACH_API_URL</Mono> and never logs
                        it. Don't commit or share that file.
                    </li>
                    <li>Claude sees what the tools return: your postings, board and gap plan. It doesn't get your CV text.</li>
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
                        <dt className="font-medium text-primary">"Login failed: 401"</dt>
                        <dd className="mt-1 text-tertiary">
                            Wrong email or password. Check <Mono>COACH_EMAIL</Mono> and <Mono>COACH_PASSWORD</Mono>, then restart Claude. The connector logs in
                            with email and password, so an account you only use through GitHub won't work.
                        </dd>
                    </div>
                    <div>
                        <dt className="font-medium text-primary">"Set COACH_EMAIL and COACH_PASSWORD"</dt>
                        <dd className="mt-1 text-tertiary">One of the two is missing from the config.</dd>
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
