import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router";
import { DotsGrid, Plus, SearchLg } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { Checkbox } from "@/components/base/checkbox/checkbox";
import { Input } from "@/components/base/input/input";
import { api } from "@/core/api";
import { ErrorState, LoadingState, cardClass } from "@/core/ui";
import { type Application, stages } from "@/features/applications/types";
import { AddPostingModal } from "@/features/postings/add-posting-modal";
import { type PostingSummary, formatDate } from "@/features/postings/types";
import { SprintBand } from "@/features/sprints/sprint-band";
import { StartSprintModal } from "@/features/sprints/start-sprint-modal";
import { errorMessage, useSprints } from "@/features/sprints/use-sprints";

const stageOf = (id: string | null | undefined) => stages.find((s) => s.id === id);

function Score({ p }: { p?: PostingSummary }) {
    return p?.analysed && p.score != null ? (
        <span className="w-24 text-sm font-semibold text-primary md:text-right">{p.score}% match</span>
    ) : (
        <span className="w-24 text-sm text-quaternary md:text-right">Not checked</span>
    );
}

const DRAG_TYPE = "application/x-position";
type Dragged = { id: string; from: "backlog" | "sprint" };

function Row({ id, keyText, title, company, deadline, posting, stage, leading, trailing, from }: {
    id: string;
    keyText?: string;
    title: string;
    company: string;
    deadline: string | null;
    posting?: PostingSummary;
    stage?: string | null;
    leading?: React.ReactNode;
    trailing?: React.ReactNode;
    /** Where the row lives; a row can be dragged to the other list. */
    from: Dragged["from"];
}) {
    const s = stageOf(stage);
    return (
        <li
            draggable
            onDragStart={(e) => {
                e.dataTransfer.setData(DRAG_TYPE, JSON.stringify({ id, from } satisfies Dragged));
                e.dataTransfer.effectAllowed = "move";
            }}
            className="flex cursor-grab items-center gap-3 rounded-lg bg-primary px-3 py-3 shadow-xs ring-1 ring-secondary active:cursor-grabbing md:px-4"
        >
            <DotsGrid className="size-4 shrink-0 text-fg-quaternary" aria-hidden="true" />
            {leading}
            <Link to={`/positions/${id}`} className="flex min-w-0 flex-1 flex-col gap-1 rounded-xs outline-focus-ring focus-visible:outline-2 md:flex-row md:items-center md:gap-4">
                <span className="w-16 shrink-0 text-xs font-medium text-quaternary">{keyText}</span>
                <span className="min-w-0 flex-1">
                    <span className="block truncate text-md font-semibold text-primary hover:underline">{title}</span>
                    <span className="block truncate text-sm text-tertiary">{company}</span>
                </span>
                <span className="flex flex-wrap items-center gap-2 md:justify-end">
                    {s && (
                        <Badge type="pill-color" size="sm" color={s.color}>
                            {s.label}
                        </Badge>
                    )}
                    <span className="text-sm text-tertiary md:w-32 md:text-right">{deadline ? `Due ${formatDate(deadline)}` : ""}</span>
                    <Score p={posting} />
                </span>
            </Link>
            {trailing}
        </li>
    );
}

export function BacklogPage() {
    const navigate = useNavigate();
    const { sprints, active, error: sprintError, reload } = useSprints();
    const [inSprint, setInSprint] = useState<Application[] | null>(null);
    const [backlog, setBacklog] = useState<PostingSummary[] | null>(null);
    const [scores, setScores] = useState<Map<string, PostingSummary>>(new Map());
    const [error, setError] = useState<string | null>(null);
    const [notice, setNotice] = useState<string | null>(null);
    const [query, setQuery] = useState("");
    const [debounced, setDebounced] = useState("");
    const [picked, setPicked] = useState<Set<string>>(new Set());
    const [starting, setStarting] = useState(false);
    const [adding, setAdding] = useState(false);
    const [busy, setBusy] = useState(false);
    const activeId = active?.id;

    useEffect(() => {
        const timer = setTimeout(() => setDebounced(query.trim()), 250);
        return () => clearTimeout(timer);
    }, [query]);

    const load = useCallback(async () => {
        setError(null);
        try {
            const params = new URLSearchParams();
            if (debounced) params.set("q", debounced);
            const [items, loose, all] = await Promise.all([
                activeId ? api.get<Application[]>(`/api/applications?sprintId=${activeId}`) : Promise.resolve([] as Application[]),
                api.get<PostingSummary[]>(`/api/backlog?${params}`),
                api.get<PostingSummary[]>("/api/postings"),
            ]);
            setInSprint(items ?? []);
            setBacklog(loose ?? []);
            setScores(new Map((all ?? []).map((p) => [p.id, p])));
            setPicked(new Set());
        } catch (e) {
            setError(errorMessage(e));
        }
    }, [activeId, debounced]);

    useEffect(() => {
        if (sprints) load();
    }, [sprints, load]);

    const toggle = (id: string) =>
        setPicked((current) => {
            const next = new Set(current);
            if (!next.delete(id)) next.add(id);
            return next;
        });

    async function addToSprint() {
        if (!active) return;
        setBusy(true);
        setNotice(null);
        try {
            await api.post(`/api/sprints/${active.id}/items`, { postingIds: [...picked] });
            await Promise.all([reload(), load()]);
        } catch (e) {
            setNotice(errorMessage(e));
        } finally {
            setBusy(false);
        }
    }

    async function removeFromSprint(postingId: string) {
        if (!active) return;
        setNotice(null);
        try {
            await api.del(`/api/sprints/${active.id}/items/${postingId}`);
            await Promise.all([reload(), load()]);
        } catch (e) {
            setNotice(errorMessage(e));
        }
    }

    // Drag a row between the sprint and the backlog. Only the running sprint takes positions.
    const [over, setOver] = useState<"sprint" | "backlog" | null>(null);
    async function addIds(ids: string[]) {
        if (!active) return;
        setNotice(null);
        try {
            await api.post(`/api/sprints/${active.id}/items`, { postingIds: ids });
            await Promise.all([reload(), load()]);
        } catch (e) {
            setNotice(errorMessage(e));
        }
    }
    const zone = (target: "sprint" | "backlog", accept: Dragged["from"], onDrop: (id: string) => void) => ({
        onDragOver: (e: React.DragEvent) => {
            if (e.dataTransfer.types.includes(DRAG_TYPE)) {
                e.preventDefault();
                setOver(target);
            }
        },
        onDragLeave: (e: React.DragEvent) => {
            if (!e.currentTarget.contains(e.relatedTarget as Node)) setOver(null);
        },
        onDrop: (e: React.DragEvent) => {
            e.preventDefault();
            setOver(null);
            const raw = e.dataTransfer.getData(DRAG_TYPE);
            if (!raw) return;
            const dragged = JSON.parse(raw) as Dragged;
            if (dragged.from === accept) onDrop(dragged.id);
        },
    });
    const zoneClass = (target: "sprint" | "backlog") =>
        `-m-2 rounded-xl p-2 transition ${over === target ? "bg-brand-primary ring-2 ring-brand" : ""}`;

    const sorted = useMemo(() => [...(inSprint ?? [])].sort((a, b) => stages.findIndex((s) => s.id === a.stage) - stages.findIndex((s) => s.id === b.stage)), [inSprint]);
    const loading = sprints === null || inSprint === null || backlog === null;

    return (
        <div className="flex w-full flex-col gap-6">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <p className="max-w-2xl text-md text-tertiary">Every position you are considering. Pull the ones you will chase this round into a sprint.</p>
                <div className="flex shrink-0 flex-wrap gap-2">
                    <Button size="md" color="secondary" href="/find">
                        Find positions
                    </Button>
                    <Button size="md" iconLeading={Plus} onPress={() => setAdding(true)}>
                        Add position
                    </Button>
                </div>
            </div>

            {notice && (
                <p role="alert" className="rounded-lg bg-error-primary px-4 py-3 text-sm text-error-primary ring-1 ring-error_subtle">
                    {notice}
                </p>
            )}

            {sprintError || error ? (
                <ErrorState message={(sprintError ?? error)!} onRetry={() => { reload(); load(); }} />
            ) : loading ? (
                <LoadingState>Loading your backlog...</LoadingState>
            ) : (
                <>
                    {active ? (
                        <div className={`flex flex-col gap-3 ${zoneClass("sprint")}`} {...zone("sprint", "backlog", (id) => addIds([id]))}>
                            <SprintBand
                                sprint={active}
                                action={
                                    <Button size="md" color="secondary" href="/board">
                                        Open board
                                    </Button>
                                }
                            />
                            {sorted.length === 0 ? (
                                <p className="rounded-lg border border-dashed border-secondary px-4 py-6 text-center text-md text-tertiary">
                                    Drag a position here, or tick positions below and add them.
                                </p>
                            ) : (
                                <ul className="flex flex-col gap-2">
                                    {sorted.map((a) => (
                                        <Row
                                            from="sprint"
                                            key={a.id}
                                            id={a.postingId}
                                            keyText={a.key}
                                            title={a.title}
                                            company={a.company}
                                            deadline={a.deadline}
                                            posting={scores.get(a.postingId)}
                                            stage={a.stage}
                                            trailing={
                                                a.stage === "saved" && (
                                                    <Button size="sm" color="tertiary" aria-label={`Move ${a.title} back to the backlog`} onPress={() => removeFromSprint(a.postingId)}>
                                                        Remove
                                                    </Button>
                                                )
                                            }
                                        />
                                    ))}
                                </ul>
                            )}
                        </div>
                    ) : (
                        <section className={`${cardClass} flex flex-col items-start gap-3 p-5 md:p-6`}>
                            <p className="text-md font-semibold text-primary">No sprint is running.</p>
                            <p className="text-md text-tertiary">A sprint is one round of applications with a goal and an end date.</p>
                            <Button size="md" onPress={() => setStarting(true)}>
                                Start a sprint
                            </Button>
                        </section>
                    )}

                    <section aria-label="Backlog" className={`flex flex-col gap-3 ${zoneClass("backlog")}`} {...zone("backlog", "sprint", (id) => removeFromSprint(id))}>
                        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                            <h2 className="text-lg font-semibold text-primary">
                                Backlog <span className="text-tertiary">({backlog.length})</span>
                            </h2>
                            <div className="sm:w-72">
                                <Input aria-label="Search the backlog" icon={SearchLg} placeholder="Search company, title or city" value={query} onChange={setQuery} />
                            </div>
                        </div>

                        {backlog.length === 0 ? (
                            <div className={`${cardClass} flex flex-col items-start gap-3 p-6`}>
                                <p className="text-md font-semibold text-primary">{debounced ? "No positions match." : "The backlog is empty."}</p>
                                <p className="text-md text-tertiary">{debounced ? "Try another search." : "Find positions or paste one you found elsewhere."}</p>
                                {!debounced && (
                                    <Button size="md" color="secondary" href="/find">
                                        Find positions
                                    </Button>
                                )}
                            </div>
                        ) : (
                            <ul className="flex flex-col gap-2">
                                {backlog.map((p) => (
                                    <Row
                                        from="backlog"
                                        key={p.id}
                                        id={p.id}
                                        keyText={p.key}
                                        title={p.title}
                                        company={p.company}
                                        deadline={p.deadline}
                                        posting={p}
                                        stage={p.stage}
                                        leading={<Checkbox aria-label={`Select ${p.title} at ${p.company}`} isSelected={picked.has(p.id)} onChange={() => toggle(p.id)} />}
                                    />
                                ))}
                            </ul>
                        )}

                        {picked.size > 0 && (
                            <div className="sticky bottom-4 z-10 flex flex-wrap items-center justify-between gap-3 rounded-xl bg-primary p-4 shadow-lg ring-1 ring-secondary_alt">
                                <p className="text-sm font-semibold text-primary">{picked.size} selected</p>
                                {active ? (
                                    <Button size="md" onPress={addToSprint} isLoading={busy}>
                                        Add {picked.size} to {active.name}
                                    </Button>
                                ) : (
                                    <Button size="md" onPress={() => setStarting(true)}>
                                        Start a sprint first
                                    </Button>
                                )}
                            </div>
                        )}
                    </section>
                </>
            )}

            <StartSprintModal
                isOpen={starting}
                count={sprints?.length ?? 0}
                onClose={() => setStarting(false)}
                onStarted={() => {
                    setStarting(false);
                    reload();
                }}
            />
            <AddPostingModal
                isOpen={adding}
                onClose={() => setAdding(false)}
                onAdded={(posting) => {
                    setAdding(false);
                    navigate(`/positions/${posting.id}`);
                }}
            />
        </div>
    );
}
