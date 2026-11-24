import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { ArrowLeft, LinkExternal01, Trash01 } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { BelgianDateInput, DateField, Field } from "./fields";
import { GapMatrix, GapPanel } from "./gap-analysis";
import { type PostingDetail, formatDate, formatDateTime, formatStagePeriod, toLocalInput } from "./types";


function safeHref(url: string | null) {
    return url && /^https?:\/\//i.test(url) ? url : null;
}

export function PostingDetailPage() {
    const { id } = useParams();
    const navigate = useNavigate();
    const [posting, setPosting] = useState<PostingDetail | null>(null);
    const [error, setError] = useState<{ message: string; notFound: boolean } | null>(null);
    const [deadline, setDeadline] = useState("");
    const [interview, setInterview] = useState("");
    const [stageStart, setStageStart] = useState("");
    const [stageEnd, setStageEnd] = useState("");
    const [stageError, setStageError] = useState<string | undefined>();
    const [saving, setSaving] = useState(false);
    const [saveMessage, setSaveMessage] = useState<string | null>(null);
    const [confirmDelete, setConfirmDelete] = useState(false);
    const [deleting, setDeleting] = useState(false);

    const load = useCallback(async () => {
        setError(null);
        try {
            const data = await api.get<PostingDetail>(`/api/postings/${id}`);
            setPosting(data);
            setDeadline(data?.deadline ?? "");
            setInterview(toLocalInput(data?.interviewAt ?? null));
            setStageStart(data?.stageStart ?? "");
            setStageEnd(data?.stageEnd ?? "");
        } catch (e) {
            const err = e instanceof ApiError ? e : null;
            setError({ message: err?.status === 404 ? "This posting does not exist." : (err?.message ?? "Something went wrong. Try again."), notFound: err?.status === 404 });
        }
    }, [id]);

    useEffect(() => {
        load();
    }, [load]);

    async function save() {
        setSaving(true);
        setSaveMessage(null);
        setStageError(undefined);
        try {
            const updated = await api.put<PostingDetail>(`/api/postings/${id}`, {
                deadline: deadline || null,
                interviewAt: interview ? new Date(interview).toISOString() : null,
                stageStart: stageStart || null,
                stageEnd: stageEnd || null,
            });
            setPosting(updated);
            setSaveMessage("Saved.");
        } catch (e) {
            const stageProblem = e instanceof ApiError ? e.fieldErrors.stageEnd : undefined;
            setStageError(stageProblem);
            setSaveMessage(stageProblem ? null : e instanceof ApiError ? e.message : "Something went wrong. Try again.");
        } finally {
            setSaving(false);
        }
    }

    async function remove() {
        setDeleting(true);
        try {
            await api.del(`/api/postings/${id}`);
            navigate("/postings/saved", { replace: true });
        } catch (e) {
            setSaveMessage(e instanceof ApiError ? e.message : "Something went wrong. Try again.");
            setDeleting(false);
            setConfirmDelete(false);
        }
    }

    const back = (
        <div>
            <Button color="link-gray" size="md" iconLeading={ArrowLeft} href="/postings/saved">
                All postings
            </Button>
        </div>
    );

    if (error) {
        return (
            <div className="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-8 md:px-8">
                {back}
                <p role="alert" className="text-md text-error-primary">
                    {error.message}
                </p>
                {!error.notFound && (
                    <div>
                        <Button color="secondary" size="md" onPress={load}>
                            Try again
                        </Button>
                    </div>
                )}
            </div>
        );
    }

    if (!posting) {
        return (
            <div className="mx-auto max-w-6xl px-4 py-8 md:px-8">
                <p role="status" className="text-md text-tertiary">
                    Loading posting...
                </p>
            </div>
        );
    }

    const href = safeHref(posting.url);
    const required = posting.requirements.filter((r) => r.importance === "required");
    const nice = posting.requirements.filter((r) => r.importance === "nice");

    return (
        <div className="mx-auto flex max-w-6xl flex-col gap-6 px-4 py-8 md:px-8">
            {back}

            <header className="flex flex-col gap-2">
                <div className="flex flex-wrap items-center gap-2">
                    <h1 className="text-display-xs font-semibold text-primary">{posting.title}</h1>
                </div>
                <p className="text-md text-tertiary">
                    {posting.company}
                    {posting.city && `, ${posting.city}`}
                    {posting.deadline && ` · Deadline ${formatDate(posting.deadline)}`}
                    {posting.interviewAt && ` · Interview ${formatDateTime(posting.interviewAt)}`}
                    {formatStagePeriod(posting.stageStart, posting.stageEnd) && ` · Stage ${formatStagePeriod(posting.stageStart, posting.stageEnd)}`}
                </p>
                {posting.stageFit === "conflict" && (
                    <p role="note" className="text-sm font-medium text-warning-primary">
                        This internship runs outside the stage period on your profile. Check the dates with your school before you apply.
                    </p>
                )}
                {href && (
                    <a href={href} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 text-sm font-semibold text-brand-secondary hover:underline">
                        Open original posting <LinkExternal01 className="size-4" aria-hidden="true" />
                    </a>
                )}
            </header>

            <div className="grid gap-6 lg:grid-cols-3">
                <div className="flex flex-col gap-6 lg:col-span-2">
                    {posting.analysis.analysed && <GapMatrix posting={posting} />}

                    <section className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
                        <h2 className="border-b border-secondary px-5 py-4 text-md font-semibold text-primary">Requirements</h2>
                        {posting.requirements.length === 0 ? (
                            <p className="px-5 py-4 text-md text-tertiary">No requirements were found in this posting.</p>
                        ) : (
                            <div className="flex flex-col gap-5 px-5 py-4">
                                {[
                                    ["Required", required],
                                    ["Nice to have", nice],
                                ].map(([label, list]) =>
                                    (list as typeof required).length === 0 ? null : (
                                        <div key={label as string}>
                                            <h3 className="text-sm font-semibold text-secondary">{label as string}</h3>
                                            <ul className="mt-2 flex list-disc flex-col gap-1.5 pl-5 text-md text-primary">
                                                {(list as typeof required).map((r) => (
                                                    <li key={r.id}>{r.text}</li>
                                                ))}
                                            </ul>
                                        </div>
                                    ),
                                )}
                            </div>
                        )}
                    </section>

                    <section className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
                        <h2 className="border-b border-secondary px-5 py-4 text-md font-semibold text-primary">Full posting</h2>
                        {/* Posting text is untrusted: rendered as plain text, never as HTML. */}
                        <p className="whitespace-pre-wrap px-5 py-4 text-md text-secondary">{posting.rawText}</p>
                    </section>
                </div>

                <div className="flex flex-col gap-6">
                    <GapPanel posting={posting} onUpdated={setPosting} />

                    <section className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
                        <h2 className="border-b border-secondary px-5 py-4 text-md font-semibold text-primary">Dates</h2>
                        <div className="flex flex-col gap-4 px-5 py-4">
                            <Field label="Deadline">
                                <DateField type="date" value={deadline} onChange={(e) => setDeadline(e.target.value)} />
                            </Field>
                            <Field label="Interview">
                                <DateField type="datetime-local" value={interview} onChange={(e) => setInterview(e.target.value)} />
                            </Field>
                            <BelgianDateInput label="Stage starts" value={stageStart} max={stageEnd || undefined} onChange={setStageStart} />
                            <BelgianDateInput label="Stage ends" value={stageEnd} min={stageStart || undefined} onChange={setStageEnd} isInvalid={!!stageError} hint={stageError} />
                            <div className="flex items-center gap-3">
                                <Button size="md" onPress={save} isLoading={saving}>
                                    Save dates
                                </Button>
                                {saveMessage && (
                                    <p role="status" className="text-sm text-tertiary">
                                        {saveMessage}
                                    </p>
                                )}
                            </div>
                        </div>
                    </section>

                    <section className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
                        <div className="flex flex-col gap-3 px-5 py-4">
                            {confirmDelete ? (
                                <>
                                    <p className="text-sm text-secondary">Delete this posting? This cannot be undone.</p>
                                    <div className="flex gap-3">
                                        <Button color="primary-destructive" size="md" onPress={remove} isLoading={deleting}>
                                            Delete
                                        </Button>
                                        <Button color="secondary" size="md" onPress={() => setConfirmDelete(false)} isDisabled={deleting}>
                                            Cancel
                                        </Button>
                                    </div>
                                </>
                            ) : (
                                <div>
                                    <Button color="secondary-destructive" size="md" iconLeading={Trash01} onPress={() => setConfirmDelete(true)}>
                                        Delete posting
                                    </Button>
                                </div>
                            )}
                        </div>
                    </section>
                </div>
            </div>
        </div>
    );
}
