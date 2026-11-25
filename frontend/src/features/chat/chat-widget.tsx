import { type FormEvent, type KeyboardEvent, useEffect, useRef, useState } from "react";
import { AlertCircle, ArrowRight, CheckCircle, ChevronRight, MessageChatCircle, Send01, XClose } from "@untitledui/icons";
import { useLocation, useNavigate } from "react-router";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { ChatMarkdown } from "./chat-markdown";
import { fieldClass } from "@/core/ui";
import { cx } from "@/utils/cx";

type Role = "user" | "assistant";
type ToolCall = { name: string; args: string; status: "ok" | "error"; summary: string; durationMs: number };
type PendingAction = { tool: string; args: Record<string, string>; summary: string };
type ActionState = { phase: "pending" | "running" | "done" | "cancelled" | "failed"; message?: string; navigateTo?: string | null };
type Message = {
    role: Role;
    content: string;
    navigateTo?: string | null;
    toolTrace?: ToolCall[];
    pendingAction?: PendingAction | null;
    action?: ActionState;
};
type ChatResponse = { reply: string; navigateTo: string | null; toolTrace: ToolCall[]; pendingAction: PendingAction | null };
type ConfirmResponse = { message: string; navigateTo: string | null };

const MAX_LENGTH = 2000;

/** Must match the server whitelist (ChatRoutes); the server is the authority, this only supplies the chip label. */
const PAGE_NAMES: Record<string, string> = {
    "/dashboard": "Home",
    "/backlog": "Backlog",
    "/board": "Board",
    "/sprints": "Sprints",
    "/gaps": "Gap plan",
    "/documents": "Documents",
    "/interview": "Interview prep",
    "/profile": "Profile",
    "/settings": "Settings",
};

/** Short labels for the tools the server may run; unknown names show as-is. */
const TOOL_LABELS: Record<string, string> = {
    get_profile: "/profile",
    list_github_projects: "/github projects",
    list_postings: "/postings",
    get_posting: "/posting",
    list_gaps: "/gaps",
    list_applications: "/applications",
    get_journey: "/journey",
    search_jobs: "/search jobs",
    set_application_stage: "/move application",
    save_job: "/save job",
    add_to_applications: "/add to applications",
};

const SUGGESTIONS = ["What should I do next?", "Which GitHub projects do you see?", "What are my biggest gaps?"];

/** What the model sees of an earlier turn: the reply plus how a proposed action ended, so it never assumes success. */
function historyContent(message: Message) {
    const { action, pendingAction } = message;
    if (!pendingAction || !action || action.phase === "pending" || action.phase === "running") return message.content;
    const outcome =
        action.phase === "done" ? `confirmed: ${action.message}` : action.phase === "cancelled" ? "cancelled by the student" : `failed: ${action.message}`;
    return `${message.content}\n\n[Proposed action "${pendingAction.summary}" ${outcome}]`;
}

function ToolTrace({ calls }: { calls: ToolCall[] }) {
    return (
        <ul aria-label="Tools used" className="flex w-full max-w-[85%] flex-col gap-1">
            {calls.map((call, index) => (
                <li key={index}>
                    <details className="group rounded-lg bg-primary text-xs ring-1 ring-secondary">
                        <summary className="flex cursor-pointer list-none items-center gap-1.5 rounded-lg px-2 py-1 text-tertiary outline-focus-ring hover:bg-secondary focus-visible:outline-2 [&::-webkit-details-marker]:hidden">
                            <ChevronRight className="size-3 shrink-0 transition-transform group-open:rotate-90" aria-hidden="true" />
                            {call.status === "ok" ? (
                                <CheckCircle className="size-3.5 shrink-0 text-fg-success-secondary" aria-label="Done" />
                            ) : (
                                <AlertCircle className="size-3.5 shrink-0 text-fg-error-secondary" aria-label="Failed" />
                            )}
                            <span className="font-mono font-medium text-secondary">{TOOL_LABELS[call.name] ?? call.name}</span>
                            <span aria-hidden="true">·</span>
                            <span className="min-w-0 truncate">{call.summary}</span>
                        </summary>
                        <dl className="grid grid-cols-[auto_1fr] gap-x-2 gap-y-0.5 border-t border-secondary px-2 py-1.5 text-tertiary">
                            <dt>Tool</dt>
                            <dd className="font-mono break-all text-secondary">{call.name}</dd>
                            <dt>Args</dt>
                            <dd className="font-mono break-all text-secondary">{call.args === "{}" ? "none" : call.args}</dd>
                            <dt>Result</dt>
                            <dd className="text-secondary">{call.summary}</dd>
                            <dt>Time</dt>
                            <dd className="text-secondary">{call.durationMs} ms</dd>
                        </dl>
                    </details>
                </li>
            ))}
        </ul>
    );
}

export function ChatWidget() {
    const { pathname } = useLocation();
    const navigate = useNavigate();
    const [open, setOpen] = useState(false);
    const [messages, setMessages] = useState<Message[]>([]);
    const [draft, setDraft] = useState("");
    const [sending, setSending] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const inputRef = useRef<HTMLTextAreaElement>(null);
    const buttonRef = useRef<HTMLButtonElement>(null);
    const listRef = useRef<HTMLDivElement>(null);
    const wasOpen = useRef(false);

    // Focus moves to the input on open and back to the launcher on close.
    useEffect(() => {
        if (open) inputRef.current?.focus();
        else if (wasOpen.current) buttonRef.current?.focus();
        wasOpen.current = open;
    }, [open]);

    useEffect(() => {
        listRef.current?.scrollTo({ top: listRef.current.scrollHeight });
    }, [messages, sending, error]);

    async function ask(history: Message[]) {
        setSending(true);
        setError(null);
        try {
            const response = await api.post<ChatResponse>("/api/chat", {
                messages: history.map((message) => ({ role: message.role, content: historyContent(message) })),
                route: pathname,
            });
            if (response)
                setMessages([
                    ...history,
                    {
                        role: "assistant",
                        content: response.reply,
                        navigateTo: response.navigateTo,
                        toolTrace: response.toolTrace ?? [],
                        pendingAction: response.pendingAction,
                        action: response.pendingAction ? { phase: "pending" } : undefined,
                    },
                ]);
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Something went wrong. Try again.");
        } finally {
            setSending(false);
        }
    }

    function setAction(index: number, action: ActionState) {
        setMessages((current) => current.map((message, i) => (i === index ? { ...message, action } : message)));
    }

    // Nothing runs until the student clicks Confirm; the server re-checks the tool, ownership and stage.
    async function confirmAction(index: number, pending: PendingAction) {
        setAction(index, { phase: "running" });
        try {
            const result = await api.post<ConfirmResponse>("/api/chat/actions/confirm", { tool: pending.tool, args: pending.args });
            setAction(index, { phase: "done", message: result?.message ?? "Done.", navigateTo: result?.navigateTo });
        } catch (e) {
            setAction(index, { phase: "failed", message: e instanceof ApiError ? e.message : "Something went wrong. Try again." });
        }
        inputRef.current?.focus();
    }

    function cancelAction(index: number) {
        setAction(index, { phase: "cancelled" });
        inputRef.current?.focus();
    }

    function send(text: string) {
        const content = text.trim().slice(0, MAX_LENGTH);
        if (!content || sending) return;
        const history = [...messages, { role: "user" as const, content }];
        setMessages(history);
        setDraft("");
        void ask(history);
    }

    const onSubmit = (event: FormEvent) => {
        event.preventDefault();
        send(draft);
    };

    const onKeyDown = (event: KeyboardEvent) => {
        if (event.key === "Escape") setOpen(false);
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            send(draft);
        }
    };

    return (
        <>
            {!open && (
                <button
                    ref={buttonRef}
                    type="button"
                    aria-label="Open assistant"
                    onClick={() => setOpen(true)}
                    className="fixed right-6 bottom-6 z-40 flex size-14 cursor-pointer items-center justify-center rounded-full bg-brand-solid text-white shadow-lg outline-focus-ring transition duration-100 ease-linear hover:bg-brand-solid_hover focus-visible:outline-2 focus-visible:outline-offset-2"
                >
                    <MessageChatCircle className="size-6" aria-hidden="true" />
                </button>
            )}

            {open && (
                <section
                    role="dialog"
                    aria-label="Assistant"
                    onKeyDown={(event) => event.key === "Escape" && setOpen(false)}
                    className="fixed inset-0 z-50 flex flex-col bg-primary shadow-xl ring-1 ring-secondary motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-4 sm:inset-auto sm:right-6 sm:bottom-6 sm:h-[560px] sm:max-h-[calc(100dvh-3rem)] sm:w-[400px] sm:rounded-xl"
                >
                    <header className="flex items-center justify-between border-b border-secondary px-4 py-3">
                        <div>
                            <h2 className="text-md font-semibold text-primary">Assistant</h2>
                            <p className="text-xs text-tertiary">Reads your profile, projects and postings. Changes need your OK.</p>
                        </div>
                        <Button size="sm" color="tertiary" iconLeading={XClose} aria-label="Close assistant" onPress={() => setOpen(false)} />
                    </header>

                    <div ref={listRef} className="flex flex-1 flex-col gap-3 overflow-y-auto px-4 py-4" aria-live="polite">
                        {messages.length === 0 && (
                            <div className="flex flex-col gap-3">
                                <p className="text-sm text-secondary">Hi! Ask about your profile, projects, postings or gaps, or how a page works.</p>
                                <div className="flex flex-wrap gap-2">
                                    {SUGGESTIONS.map((suggestion) => (
                                        <button
                                            key={suggestion}
                                            type="button"
                                            onClick={() => send(suggestion)}
                                            className="cursor-pointer rounded-full bg-primary px-3 py-1.5 text-sm font-medium text-secondary ring-1 ring-primary transition hover:bg-secondary"
                                        >
                                            {suggestion}
                                        </button>
                                    ))}
                                </div>
                            </div>
                        )}

                        {messages.map((message, index) => (
                            <div key={index} className={message.role === "user" ? "flex flex-col items-end gap-2" : "flex flex-col items-start gap-2"}>
                                {message.toolTrace && message.toolTrace.length > 0 && <ToolTrace calls={message.toolTrace} />}
                                {message.role === "user" ? (
                                    <p className="max-w-[85%] rounded-xl rounded-br-sm bg-brand-solid px-3 py-2 text-sm whitespace-pre-wrap text-white">
                                        {message.content}
                                    </p>
                                ) : (
                                    <div className="max-w-[85%] min-w-0 rounded-xl rounded-bl-sm bg-secondary px-3 py-2 text-sm break-words text-primary">
                                        <ChatMarkdown
                                            source={message.content}
                                            onNavigate={(path) => {
                                                navigate(path);
                                                setOpen(false);
                                            }}
                                        />
                                    </div>
                                )}
                                {message.navigateTo && PAGE_NAMES[message.navigateTo] && (
                                    <Button
                                        size="sm"
                                        color="secondary"
                                        iconTrailing={ArrowRight}
                                        onPress={() => {
                                            navigate(message.navigateTo!);
                                            setOpen(false);
                                        }}
                                    >
                                        Go to {PAGE_NAMES[message.navigateTo]}
                                    </Button>
                                )}
                                {message.pendingAction && message.action && (
                                    <div
                                        role="group"
                                        aria-label="Action needs your confirmation"
                                        className="flex w-full max-w-[85%] flex-col gap-2 rounded-xl bg-primary p-3 text-sm shadow-xs ring-1 ring-brand"
                                    >
                                        <p className="font-medium text-primary">{message.pendingAction.summary}</p>
                                        {(message.action.phase === "pending" || message.action.phase === "running") && (
                                            <div className="flex gap-2">
                                                <Button
                                                    size="sm"
                                                    color="primary"
                                                    isLoading={message.action.phase === "running"}
                                                    isDisabled={message.action.phase === "running"}
                                                    onPress={() => void confirmAction(index, message.pendingAction!)}
                                                >
                                                    Confirm
                                                </Button>
                                                <Button
                                                    size="sm"
                                                    color="secondary"
                                                    isDisabled={message.action.phase === "running"}
                                                    onPress={() => cancelAction(index)}
                                                >
                                                    Cancel
                                                </Button>
                                            </div>
                                        )}
                                        {message.action.phase === "cancelled" && <p className="text-tertiary">Cancelled. Nothing changed.</p>}
                                        {message.action.phase === "failed" && (
                                            <p role="alert" className="text-error-primary">
                                                {message.action.message}
                                            </p>
                                        )}
                                        {message.action.phase === "done" && (
                                            <div className="flex flex-wrap items-center gap-2">
                                                <p className="flex items-center gap-1.5 text-success-primary">
                                                    <CheckCircle className="size-4" aria-hidden="true" />
                                                    {message.action.message}
                                                </p>
                                                {message.action.navigateTo && PAGE_NAMES[message.action.navigateTo] && (
                                                    <Button
                                                        size="sm"
                                                        color="link-color"
                                                        iconTrailing={ArrowRight}
                                                        onPress={() => {
                                                            navigate(message.action!.navigateTo!);
                                                            setOpen(false);
                                                        }}
                                                    >
                                                        Open {PAGE_NAMES[message.action.navigateTo]}
                                                    </Button>
                                                )}
                                            </div>
                                        )}
                                    </div>
                                )}
                            </div>
                        ))}

                        {sending && (
                            <div role="status" className="flex w-fit items-center gap-1 rounded-xl bg-secondary px-3 py-2 text-xs text-tertiary">
                                <span className="mr-1">Thinking… may look up your data</span>
                                {[0, 150, 300].map((delay) => (
                                    <span
                                        key={delay}
                                        style={{ animationDelay: `${delay}ms` }}
                                        className="size-1.5 rounded-full bg-fg-quaternary motion-safe:animate-bounce"
                                    />
                                ))}
                            </div>
                        )}

                        {error && (
                            <div role="alert" className="flex items-center justify-between gap-3 rounded-lg bg-error-primary px-3 py-2 text-sm text-error-primary">
                                <span>{error}</span>
                                <Button size="sm" color="secondary" onPress={() => void ask(messages)}>
                                    Try again
                                </Button>
                            </div>
                        )}
                    </div>

                    <form onSubmit={onSubmit} className="flex items-end gap-2 border-t border-secondary p-3">
                        <label htmlFor="chat-input" className="sr-only">
                            Message
                        </label>
                        <textarea
                            id="chat-input"
                            ref={inputRef}
                            value={draft}
                            rows={1}
                            maxLength={MAX_LENGTH}
                            onChange={(event) => setDraft(event.target.value)}
                            onKeyDown={onKeyDown}
                            placeholder="Ask a question"
                            className={cx(fieldClass, "max-h-32 min-h-10 flex-1 resize-none")}
                        />
                        <Button type="submit" size="md" iconLeading={Send01} aria-label="Send" isDisabled={sending || !draft.trim()} />
                    </form>
                </section>
            )}
        </>
    );
}
