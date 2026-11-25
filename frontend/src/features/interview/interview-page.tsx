import { useCallback, useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router";
import { AlertCircle, MessageChatCircle, RefreshCw01 } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import type { Journey } from "@/features/journey/journey-bar";
import type { PostingSummary } from "@/features/postings/types";
import {
  cardClass,
  ErrorState,
  fieldClass,
  LoadingState,
  PageHeader,
} from "@/core/ui";

type Answer = {
  id: string;
  answer: string;
  score: number | null;
  good: string;
  add: string;
  example: string;
  createdAt: string;
};
type Question = {
  id: string;
  source: "gap" | "repo" | "company";
  text: string;
  project: string | null;
  gap: string | null;
  answers: Answer[];
};
type Interview = {
  postingId: string;
  company: string;
  title: string;
  language: string;
  analysed: boolean;
  questions: Question[];
};

const MAX_ANSWER = 3000;
const MAX_TRIES = 5;
const sourceLabel: Record<Question["source"], string> = {
  gap: "From a gap",
  repo: "From your work",
  company: "Asked at this company",
};
const sourceColor = { gap: "warning", repo: "brand", company: "gray" } as const;
const scoreLabel = [
  "",
  "Off track",
  "Needs work",
  "Fine, but vague",
  "Good",
  "Interview-ready",
];
const scoreColor = (score: number) =>
  score >= 4 ? "success" : score === 3 ? "warning" : "error";

const panelClass = `${cardClass} p-5`;

const errorText = (error: unknown) =>
  error instanceof ApiError
    ? error.message
    : "Could not reach the server. Check your connection and try again.";

function why(question: Question, company: string) {
  if (question.source === "gap")
    return question.gap
      ? `Asked because the posting wants "${question.gap}" and your profile does not fully show it yet.`
      : "Asked because of a gap in your profile.";
  if (question.source === "repo")
    return `Asked about ${question.project?.replace(/^new:/, "")}, from your own work.`;
  return `Other candidates were asked this at ${company}.`;
}

function Feedback({ answer }: { answer: Answer }) {
  return (
    <div className="flex flex-col gap-3 rounded-lg bg-secondary p-4 text-sm">
      {answer.score !== null && (
        <p className="flex flex-wrap items-center gap-2">
          <span className="font-semibold text-primary">
            Score {answer.score} of 5
          </span>
          <Badge size="sm" color={scoreColor(answer.score)}>
            {scoreLabel[answer.score]}
          </Badge>
        </p>
      )}
      <div>
        <p className="font-semibold text-primary">What was good</p>
        <p className="text-secondary">{answer.good}</p>
      </div>
      <div>
        <p className="font-semibold text-primary">What to add</p>
        <p className="text-secondary">{answer.add}</p>
      </div>
      <div>
        <p className="font-semibold text-primary">You could say</p>
        <p className="text-secondary italic">&ldquo;{answer.example}&rdquo;</p>
      </div>
      <details className="text-tertiary">
        <summary className="cursor-pointer rounded outline-focus-ring focus-visible:outline-2">
          Your answer
        </summary>
        <p className="mt-1 whitespace-pre-wrap">{answer.answer}</p>
      </details>
    </div>
  );
}

function QuestionCard({
  interview,
  question,
  index,
  onAnswered,
}: {
  interview: Interview;
  question: Question;
  index: number;
  onAnswered: (questionId: string, answer: Answer) => void;
}) {
  const latest = question.answers[0];
  const triesLeft = MAX_TRIES - question.answers.length;
  const [editing, setEditing] = useState(!latest);
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const boxRef = useRef<HTMLTextAreaElement>(null);
  const id = `a-${question.id}`;

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      const answer = await api.post<Answer>("/api/interview/answers", {
        postingId: interview.postingId,
        questionId: question.id,
        answer: text,
      });
      if (answer) {
        onAnswered(question.id, answer);
        setText("");
        setEditing(false);
      }
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  };

  const tryAgain = () => {
    setText(latest?.answer ?? "");
    setEditing(true);
    requestAnimationFrame(() => boxRef.current?.focus());
  };

  return (
    <li
      className={`flex flex-col gap-4 ${panelClass} sm:p-6`}
      aria-labelledby={`q-${question.id}`}
    >
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-semibold text-tertiary">
          Question {index + 1}
        </span>
        <Badge size="sm" color={sourceColor[question.source]}>
          {sourceLabel[question.source]}
        </Badge>
        {latest && (
          <Badge size="sm" color="success">
            Practised
          </Badge>
        )}
      </div>
      <div className="flex flex-col gap-1.5">
        <p
          id={`q-${question.id}`}
          className="text-lg font-semibold text-primary"
        >
          {question.text}
        </p>
        <p className="text-sm text-tertiary">
          {why(question, interview.company)}
        </p>
      </div>

      {editing && (
        <>
          <div className="flex flex-col gap-1.5">
            <label htmlFor={id} className="text-sm font-medium text-secondary">
              {latest ? "Your better answer" : "Your answer"}
            </label>
            <textarea
              ref={boxRef}
              id={id}
              className={fieldClass}
              rows={5}
              maxLength={MAX_ANSWER}
              value={text}
              disabled={busy}
              onChange={(e) => setText(e.target.value)}
              placeholder="Answer as you would in the interview. Name your own project and what you did."
              aria-describedby={`${id}-hint`}
            />
            <p id={`${id}-hint`} className="text-xs text-tertiary">
              {text.length} / {MAX_ANSWER} characters. {triesLeft}{" "}
              {triesLeft === 1 ? "try" : "tries"} left for this question.
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button
              size="md"
              onPress={submit}
              isLoading={busy}
              isDisabled={busy || text.trim().length === 0}
              showTextWhileLoading
            >
              {busy ? "Reading your answer..." : "Get feedback"}
            </Button>
            {latest && !busy && (
              <Button
                size="md"
                color="tertiary"
                onPress={() => setEditing(false)}
              >
                Cancel
              </Button>
            )}
          </div>
        </>
      )}

      {error && (
        <p
          role="alert"
          className="flex items-start gap-2 text-sm text-error-primary"
        >
          <AlertCircle className="mt-0.5 size-4 shrink-0" aria-hidden /> {error}
        </p>
      )}

      <div aria-live="polite" className="flex flex-col gap-3">
        {latest && !editing && (
          <>
            <Feedback answer={latest} />
            <div className="flex flex-wrap items-center gap-3">
              {triesLeft > 0 ? (
                <Button size="sm" color="secondary" onPress={tryAgain}>
                  Try again
                </Button>
              ) : (
                <p className="text-sm text-tertiary">
                  You used all {MAX_TRIES} tries. Move on to another question.
                </p>
              )}
              {question.answers.length > 1 && (
                <p className="text-xs text-tertiary">
                  Attempt {question.answers.length}
                  {question.answers[1].score !== null &&
                    latest.score !== null &&
                    `: score went from ${question.answers[1].score} to ${latest.score}`}
                </p>
              )}
            </div>
          </>
        )}
      </div>
    </li>
  );
}

export function InterviewPage() {
  const [params, setParams] = useSearchParams();
  const [postings, setPostings] = useState<PostingSummary[] | null>(null);
  const [best, setBest] = useState<Journey["bestPosting"]>(null);
  const [postingsError, setPostingsError] = useState<string | null>(null);
  const [interview, setInterview] = useState<Interview | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<{
    message: string;
    status: number;
  } | null>(null);
  const [confirmNew, setConfirmNew] = useState(false);
  const inFlight = useRef<string | null>(null);
  const postingId = params.get("postingId") ?? params.get("posting") ?? "";
  const company =
    postings?.find((p) => p.id === postingId)?.company ??
    interview?.company ??
    "this posting";

  const loadPostings = useCallback(() => {
    setPostingsError(null);
    Promise.all([
      api.get<PostingSummary[]>("/api/postings"),
      api.get<Journey>("/api/journey").catch(() => null),
    ])
      .then(([list, journey]) => {
        setPostings(list ?? []);
        setBest(journey?.bestPosting ?? null);
      })
      .catch((e) => setPostingsError(errorText(e)));
  }, []);
  useEffect(loadPostings, [loadPostings]);

  // No posting in the URL: open the best-scoring analysed posting, else the newest one.
  useEffect(() => {
    if (!postingId && postings && postings.length > 0)
      setParams({ postingId: best?.id ?? postings[0].id }, { replace: true });
  }, [postingId, postings, best, setParams]);

  const run = useCallback(
    async (
      id: string,
      label: string,
      call: () => Promise<Interview | null>,
    ) => {
      // Dev StrictMode runs effects twice; the first visit may cost an AI call, so never send the same request twice.
      if (inFlight.current === id) return;
      inFlight.current = id;
      setBusy(label);
      setError(null);
      try {
        const data = await call();
        if (data) setInterview(data);
      } catch (e) {
        setError({
          message: errorText(e),
          status: e instanceof ApiError ? e.status : 0,
        });
      } finally {
        inFlight.current = null;
        setBusy(null);
      }
    },
    [],
  );

  const loadQuestions = useCallback(
    (id: string, forCompany: string) =>
      run(
        id,
        `Writing 6 questions for ${forCompany}. This takes about 15 seconds...`,
        () => api.get<Interview>(`/api/interview/${id}/questions`),
      ),
    [run],
  );

  useEffect(() => {
    setInterview(null);
    setConfirmNew(false);
    if (postingId && postings)
      loadQuestions(
        postingId,
        postings.find((p) => p.id === postingId)?.company ?? "this posting",
      );
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reload only when the posting changes, not when the list object does
  }, [postingId, postings === null, loadQuestions]);

  const analyseThenGenerate = () =>
    run(
      postingId,
      `Analysing the posting for ${company}. Then the questions are written. This takes about 30 seconds...`,
      async () => {
        await api.post(`/api/postings/${postingId}/analyse`);
        setBusy(
          `Writing 6 questions for ${company}. This takes about 15 seconds...`,
        );
        return api.get<Interview>(`/api/interview/${postingId}/questions`);
      },
    );

  const regenerate = () => {
    setConfirmNew(false);
    run(postingId, `Writing 6 new questions for ${company}...`, () =>
      api.post<Interview>(`/api/interview/${postingId}/questions/regenerate`),
    );
  };

  const onAnswered = (questionId: string, answer: Answer) =>
    setInterview(
      (current) =>
        current && {
          ...current,
          questions: current.questions.map((q) =>
            q.id === questionId ? { ...q, answers: [answer, ...q.answers] } : q,
          ),
        },
    );

  const questions = interview?.questions ?? [];
  const practised = questions.filter((q) => q.answers.length > 0).length;

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 py-8 md:px-8">
      <PageHeader
        title="Interview prep"
        description="Practise the questions this company is likely to ask you, built from your gaps and your own work."
      />

      {postingsError ? (
        <ErrorState message={postingsError} onRetry={loadPostings} />
      ) : postings === null ? (
        <LoadingState>Loading your postings...</LoadingState>
      ) : postings.length === 0 ? (
        <div className={`flex flex-col items-start gap-3 ${panelClass} p-6`}>
          <MessageChatCircle
            className="size-8 text-fg-quaternary"
            aria-hidden
          />
          <p className="text-md font-semibold text-primary">
            Save a posting first
          </p>
          <p className="text-sm text-tertiary">
            Interview questions are written for one posting. Save one and come
            back here.
          </p>
          <Button size="md" href="/postings">
            Find postings
          </Button>
        </div>
      ) : (
        <>
          <div className="flex flex-col gap-3">
            <div className="flex flex-col gap-1.5">
              <label
                htmlFor="posting"
                className="text-sm font-medium text-secondary"
              >
                Practise for
              </label>
              <select
                id="posting"
                className={fieldClass}
                value={postingId}
                disabled={busy !== null}
                onChange={(e) => setParams({ postingId: e.target.value })}
              >
                {postings.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.title} at {p.company}
                    {p.id === best?.id ? ` (best match, ${best.score}%)` : ""}
                  </option>
                ))}
              </select>
            </div>

            <div aria-live="polite">
              {busy && (
                <p
                  role="status"
                  className={`flex items-center gap-2 text-sm text-secondary ${panelClass}`}
                >
                  <RefreshCw01
                    className="size-4 shrink-0 animate-spin text-fg-brand-primary motion-reduce:animate-none"
                    aria-hidden
                  />{" "}
                  {busy}
                </p>
              )}
            </div>
          </div>

          {error && !busy && (
            <div
              role="alert"
              className={`flex flex-col items-start gap-3 ${panelClass}`}
            >
              <p className="flex items-start gap-2 text-sm text-error-primary">
                <AlertCircle className="mt-0.5 size-4 shrink-0" aria-hidden />{" "}
                {error.message}
              </p>
              <div className="flex flex-wrap gap-2">
                <Button
                  size="sm"
                  color="secondary"
                  onPress={() => loadQuestions(postingId, company)}
                >
                  Try again
                </Button>
                {error.status === 409 && (
                  <Button size="sm" color="tertiary" href="/profile">
                    Go to your profile
                  </Button>
                )}
                <Button
                  size="sm"
                  color="tertiary"
                  href={`/postings/${postingId}`}
                >
                  Open the posting
                </Button>
              </div>
            </div>
          )}

          {interview && !busy && !interview.analysed && (
            <div
              className={`flex flex-col items-start gap-3 ${panelClass} p-6`}
            >
              <p className="text-md font-semibold text-primary">
                Analyse this posting first
              </p>
              <p className="text-sm text-tertiary">
                The questions come from what {interview.company} asks for and
                what your profile is missing. Analyse the posting, then 6
                questions are written for you. This uses two AI calls.
              </p>
              <Button size="md" onPress={analyseThenGenerate}>
                Analyse and write questions
              </Button>
            </div>
          )}

          {interview && !busy && questions.length > 0 && (
            <section aria-label="Questions" className="flex flex-col gap-4">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="flex min-w-48 flex-1 flex-col gap-1.5">
                  <p
                    className="text-sm font-medium text-secondary"
                    id="progress-label"
                  >
                    {practised} of {questions.length} practised
                  </p>
                  <div
                    className="h-2 w-full max-w-xs overflow-hidden rounded-full bg-tertiary"
                    role="progressbar"
                    aria-labelledby="progress-label"
                    aria-valuemin={0}
                    aria-valuemax={questions.length}
                    aria-valuenow={practised}
                  >
                    <div
                      className="h-full rounded-full bg-fg-success-primary transition-all"
                      style={{
                        width: `${(practised / questions.length) * 100}%`,
                      }}
                    />
                  </div>
                </div>
                {interview.analysed && !confirmNew && (
                  <Button
                    size="sm"
                    color="secondary"
                    iconLeading={RefreshCw01}
                    onPress={() => setConfirmNew(true)}
                  >
                    New questions
                  </Button>
                )}
              </div>

              {confirmNew && (
                <div
                  role="alertdialog"
                  aria-labelledby="confirm-new"
                  className={`flex flex-col items-start gap-3 ${panelClass}`}
                >
                  <p id="confirm-new" className="text-sm text-secondary">
                    Write a new set of questions? This uses an AI call and
                    removes your answers to the current{" "}
                    {questions.filter((q) => q.source !== "company").length}{" "}
                    questions. Company questions and their answers stay.
                  </p>
                  <div className="flex flex-wrap gap-2">
                    <Button size="sm" onPress={regenerate}>
                      Write new questions
                    </Button>
                    <Button
                      size="sm"
                      color="tertiary"
                      onPress={() => setConfirmNew(false)}
                      autoFocus
                    >
                      Keep these
                    </Button>
                  </div>
                </div>
              )}

              <ol className="flex flex-col gap-4">
                {questions.map((q, i) => (
                  <QuestionCard
                    key={q.id}
                    interview={interview}
                    question={q}
                    index={i}
                    onAnswered={onAnswered}
                  />
                ))}
              </ol>
            </section>
          )}
        </>
      )}
    </div>
  );
}
