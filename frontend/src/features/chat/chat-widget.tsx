import { type FormEvent, type KeyboardEvent, useEffect, useRef, useState } from "react";
import { ArrowRight, MessageChatCircle, Send01, XClose } from "@untitledui/icons";
import { useLocation, useNavigate } from "react-router";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { fieldClass } from "@/core/ui";
import { cx } from "@/utils/cx";

type Role = "user" | "assistant";
type Message = { role: Role; content: string; navigateTo?: string | null };
type ChatResponse = { reply: string; navigateTo: string | null };

const MAX_LENGTH = 2000;

/** Must match the server whitelist (ChatRoutes); the server is the authority, this only supplies the chip label. */
const PAGE_NAMES: Record<string, string> = {
    "/dashboard": "Dashboard",
    "/postings": "Postings",
    "/gaps": "Gap plan",
    "/documents": "Documents",
    "/interview": "Interview prep",
    "/applications": "Applications",
    "/profile": "Profile",
    "/settings": "Settings",
};

const SUGGESTIONS = ["Where do I add a posting?", "What is the gap plan?"];

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
                messages: history.map(({ role, content }) => ({ role, content })),
                route: pathname,
            });
            if (response) setMessages([...history, { role: "assistant", content: response.reply, navigateTo: response.navigateTo }]);
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Something went wrong. Try again.");
        } finally {
            setSending(false);
        }
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
                            <p className="text-xs text-tertiary">Ask how the app works. It cannot see your data yet.</p>
                        </div>
                        <Button size="sm" color="tertiary" iconLeading={XClose} aria-label="Close assistant" onPress={() => setOpen(false)} />
                    </header>

                    <div ref={listRef} className="flex flex-1 flex-col gap-3 overflow-y-auto px-4 py-4" aria-live="polite">
                        {messages.length === 0 && (
                            <div className="flex flex-col gap-3">
                                <p className="text-sm text-secondary">Hi! Ask me where to find something or how a page works.</p>
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
                                <p
                                    className={
                                        message.role === "user"
                                            ? "max-w-[85%] rounded-xl rounded-br-sm bg-brand-solid px-3 py-2 text-sm whitespace-pre-wrap text-white"
                                            : "max-w-[85%] rounded-xl rounded-bl-sm bg-secondary px-3 py-2 text-sm whitespace-pre-wrap text-primary"
                                    }
                                >
                                    {message.content}
                                </p>
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
                            </div>
                        ))}

                        {sending && (
                            <div role="status" aria-label="Assistant is typing" className="flex w-fit items-center gap-1 rounded-xl bg-secondary px-3 py-3">
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
