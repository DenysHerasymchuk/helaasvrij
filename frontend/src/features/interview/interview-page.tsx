import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router";
import { AlertCircle, CheckCircle, MessageChatCircle } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import type { PostingSummary } from "@/features/postings/types";

type Answer = { id: string; answer: string; good: string; add: string; example: string; createdAt: string };
type Question = { id: string; source: "gap" | "repo" | "company"; text: string; project: string | null; answers: Answer[] };
type Interview = { postingId: string; company: string; title: string; language: string; questions: Question[] };

const MAX_ANSWER = 3000;
const sourceLabel: Record<Question["source"], string> = { gap: "From a gap", repo: "From your repo", company: "Asked at this company" };
const sourceColor = { gap: "warning", repo: "brand", company: "gray" } as const;

const fieldClass =
    "w-full rounded-lg bg-primary px-3.5 py-2.5 text-md text-primary shadow-xs ring-1 ring-primary ring-inset placeholder:text-placeholder outline-none focus:ring-2 focus:ring-brand";

const errorText = (error: unknown) => (error instanceof ApiError ? error.message : "Something went wrong. Try again.");

function Feedback({ answer }: { answer: Answer }) {
    return (
        <div className="flex flex-col gap-3 rounded-lg bg-secondary p-4 text-sm" aria-live="polite">
            <div>
                <p className="font-semibold text-primary">What was good</p>
                <p className="text-secondary">{answer.good}</p>
            </div>
            <div>
                <p className="font-semibold text-primary">What to add</p>
                <p className="text-secondary">{answer.add}</p>
            </div>
            <div>
                <p className="font-semibold text-primary">Example sentence</p>
                <p className="text-secondary italic">&ldquo;{answer.example}&rdquo;</p>
            </div>
        </div>
    );
}

function QuestionCard({ postingId, question, onAnswered }: { postingId: string; question: Question; onAnswered: (questionId: string, answer: Answer) => void }) {
    const [text, setText] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const latest = question.answers[0];

    const submit = async () => {
        setBusy(true);
        setError(null);
        try {
            const answer = await api.post<Answer>("/api/interview/answers", { postingId, questionId: question.id, answer: text });
            if (answer) {
                onAnswered(question.id, answer);
                setText("");
            }
        } catch (e) {
            setError(errorText(e));
        } finally {
            setBusy(false);
        }
    };

    return (
        <li className="flex flex-col gap-4 rounded-xl bg-primary p-4 shadow-xs ring-1 ring-secondary sm:p-5">
            <div className="flex flex-wrap items-center gap-2">
                <Badge size="sm" color={sourceColor[question.source]}>{sourceLabel[question.source]}</Badge>
                {question.project && <Badge size="sm" color="gray">{question.project}</Badge>}
            </div>
            <p className="text-md font-semibold text-primary">{question.text}</p>

            <div className="flex flex-col gap-1.5">
                <label htmlFor={`a-${question.id}`} className="text-sm font-medium text-secondary">
                    Your answer
                </label>
                <textarea
                    id={`a-${question.id}`}
                    className={fieldClass}
                    rows={4}
                    maxLength={MAX_ANSWER}
                    value={text}
                    disabled={busy}
                    onChange={(e) => setText(e.target.value)}
                    placeholder="Answer as you would in the interview. Mention your own work."
                />
                <p className="text-xs text-tertiary">{text.length} / {MAX_ANSWER}</p>
            </div>

            {error && (
                <p role="alert" className="flex items-start gap-2 text-sm text-error-primary">
                    <AlertCircle className="mt-0.5 size-4 shrink-0" /> {error}
                </p>
            )}
            <div>
                <Button size="md" onPress={submit} isLoading={busy} isDisabled={busy || text.trim().length === 0} showTextWhileLoading>
                    {busy ? "Getting feedback..." : latest ? "Get feedback again" : "Get feedback"}
                </Button>
            </div>

            {latest && (
                <div className="flex flex-col gap-2">
                    <p className="flex items-center gap-1.5 text-sm font-semibold text-primary">
                        <CheckCircle className="size-4 text-fg-success-primary" /> Feedback on your last answer
                    </p>
                    <Feedback answer={latest} />
                    {question.answers.length > 1 && <p className="text-xs text-tertiary">{question.answers.length} attempts so far.</p>}
                </div>
            )}
        </li>
    );
}

export function InterviewPage() {
    const [params, setParams] = useSearchParams();
    const [postings, setPostings] = useState<PostingSummary[] | null>(null);
    const [postingsError, setPostingsError] = useState<string | null>(null);
    const [interview, setInterview] = useState<Interview | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const postingId = params.get("posting") ?? "";

    const loadPostings = useCallback(() => {
        setPostingsError(null);
        api.get<PostingSummary[]>("/api/postings")
            .then((list) => setPostings(list ?? []))
            .catch((e) => setPostingsError(errorText(e)));
    }, []);
    useEffect(loadPostings, [loadPostings]);

    const loadQuestions = useCallback((id: string) => {
        setLoading(true);
        setError(null);
        setInterview(null);
        api.get<Interview>(`/api/interview/${id}/questions`)
            .then((data) => setInterview(data))
            .catch((e) => setError(errorText(e)))
            .finally(() => setLoading(false));
    }, []);

    useEffect(() => {
        if (postingId) loadQuestions(postingId);
        else {
            setInterview(null);
            setError(null);
        }
    }, [postingId, loadQuestions]);

    const onAnswered = (questionId: string, answer: Answer) =>
        setInterview((current) =>
            current && {
                ...current,
                questions: current.questions.map((q) => (q.id === questionId ? { ...q, answers: [answer, ...q.answers] } : q)),
            },
        );

    return (
        <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 py-8 md:px-8">
            <header className="flex flex-col gap-1">
                <h1 className="text-display-xs font-semibold text-primary">Interview prep</h1>
                <p className="text-md text-tertiary">Practice questions built from your gaps and your own repos. Pick a posting to start.</p>
            </header>

            {postingsError ? (
                <div role="alert" className="flex flex-col items-start gap-3 rounded-xl bg-primary p-5 ring-1 ring-secondary">
                    <p className="text-sm text-error-primary">{postingsError}</p>
                    <Button size="sm" color="secondary" onPress={loadPostings}>Try again</Button>
                </div>
            ) : postings === null ? (
                <p className="text-sm text-tertiary">Loading your postings...</p>
            ) : postings.length === 0 ? (
                <div className="flex flex-col items-start gap-3 rounded-xl bg-primary p-6 ring-1 ring-secondary">
                    <MessageChatCircle className="size-8 text-fg-quaternary" />
                    <p className="text-md font-semibold text-primary">No postings yet</p>
                    <p className="text-sm text-tertiary">Add a posting and run its gap analysis. Then the questions appear here.</p>
                    <Button size="md" href="/postings">Go to postings</Button>
                </div>
            ) : (
                <>
                    <div className="flex flex-col gap-1.5">
                        <label htmlFor="posting" className="text-sm font-medium text-secondary">Posting</label>
                        <select id="posting" className={fieldClass} value={postingId} onChange={(e) => setParams(e.target.value ? { posting: e.target.value } : {})}>
                            <option value="">Choose a posting...</option>
                            {postings.map((p) => (
                                <option key={p.id} value={p.id}>{p.title} at {p.company}</option>
                            ))}
                        </select>
                    </div>

                    {!postingId && <p className="text-sm text-tertiary">Choose a posting to see your questions.</p>}

                    {loading && (
                        <p role="status" className="rounded-xl bg-primary p-5 text-sm text-tertiary ring-1 ring-secondary">
                            Writing your questions. The first time this takes around ten seconds...
                        </p>
                    )}

                    {error && (
                        <div role="alert" className="flex flex-col items-start gap-3 rounded-xl bg-primary p-5 ring-1 ring-secondary">
                            <p className="flex items-start gap-2 text-sm text-error-primary">
                                <AlertCircle className="mt-0.5 size-4 shrink-0" /> {error}
                            </p>
                            <div className="flex flex-wrap gap-2">
                                <Button size="sm" color="secondary" onPress={() => loadQuestions(postingId)}>Try again</Button>
                                <Button size="sm" color="tertiary" href={`/postings/${postingId}`}>Open the posting</Button>
                            </div>
                        </div>
                    )}

                    {interview && interview.questions.length === 0 && <p className="text-sm text-tertiary">No questions for this posting yet.</p>}
                    {interview && interview.questions.length > 0 && (
                        <ul className="flex flex-col gap-4">
                            {interview.questions.map((q) => (
                                <QuestionCard key={q.id} postingId={interview.postingId} question={q} onAnswered={onAnswered} />
                            ))}
                        </ul>
                    )}
                </>
            )}
        </div>
    );
}
