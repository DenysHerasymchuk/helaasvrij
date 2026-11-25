import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { type Coverage, type PostingSummary, formatDate } from "@/features/postings/types";
import { itemsInCard, PageFrame, PageHeader } from "@/core/ui";
import { cx } from "@/utils/cx";

type Gap = {
    requirementId: string;
    postingId: string;
    company: string;
    postingTitle: string;
    requirement: string;
    importance: "required" | "nice";
    coverage: Coverage;
    action: string | null;
    project: string | null;
    dueDate: string | null;
    done: boolean;
    impact: number;
    scoreGain: number;
    postingScore: number;
};

type Rescan = {
    gap: Gap;
    oldScore: number;
    newScore: number;
    oldCoverage: Coverage;
    newCoverage: Coverage;
    newItemsToReview: number;
    unverifiableEvidence: number;
};

type Notice = { kind: "success" | "error"; text: string; review?: number };
type Status = "open" | "done" | "all";

const coverageLabel: Record<Coverage, string> = { strong: "Strong", partial: "Partial", none: "Not shown" };
const message = (e: unknown) => (e instanceof ApiError ? e.message : "Something went wrong. Try again.");
const statuses: { id: Status; label: string }[] = [
    { id: "open", label: "Open" },
    { id: "done", label: "Done" },
    { id: "all", label: "All" },
];

const railButton = (active: boolean) =>
    cx(
        "flex w-full cursor-pointer flex-col gap-0.5 rounded-lg px-3 py-2 text-left outline-focus-ring transition focus-visible:outline-2",
        active ? "bg-secondary ring-1 ring-secondary_alt" : "hover:bg-primary_hover",
    );

/** One screen: positions on the left, the checklist for the chosen position on the right. */
export function GapsPage() {
    const [gaps, setGaps] = useState<Gap[] | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [busyId, setBusyId] = useState<string | null>(null);
    const [notice, setNotice] = useState<Notice | null>(null);
    const [status, setStatus] = useState<Status>("open");
    const [params] = useSearchParams();
    const [postingId, setPostingId] = useState(params.get("postingId") ?? "all");
    const [saved, setSaved] = useState<PostingSummary[] | null>(null);
    const [analysing, setAnalysing] = useState<string | null>(null);
    const [analyseError, setAnalyseError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setError(null);
        try {
            const [g, p] = await Promise.all([api.get<Gap[]>("/api/gaps"), api.get<PostingSummary[]>("/api/postings")]);
            setGaps(g ?? []);
            setSaved(p ?? []);
        } catch (e) {
            setError(message(e));
        }
    }, []);

    useEffect(() => {
        void load();
    }, [load]);

    // One entry per analysed position, with its open gap count and score.
    const rail = useMemo(() => {
        const map = new Map<string, { id: string; company: string; title: string; open: number; score: number }>();
        for (const g of gaps ?? []) {
            const entry = map.get(g.postingId) ?? { id: g.postingId, company: g.company, title: g.postingTitle, open: 0, score: g.postingScore };
            entry.open += g.done ? 0 : 1;
            entry.score = g.postingScore;
            map.set(g.postingId, entry);
        }
        return [...map.values()];
    }, [gaps]);

    const visible = useMemo(
        () => (gaps ?? []).filter((g) => (status === "all" || (status === "done") === g.done) && (postingId === "all" || g.postingId === postingId)),
        [gaps, status, postingId],
    );

    const groups = useMemo(() => {
        const map = new Map<string, Gap[]>();
        for (const g of visible) map.set(`${g.company}: ${g.postingTitle}`, [...(map.get(`${g.company}: ${g.postingTitle}`) ?? []), g]);
        return [...map.entries()];
    }, [visible]);

    const pending = (saved ?? []).filter((p) => !p.analysed);
    const totalOpen = rail.reduce((sum, r) => sum + r.open, 0);

    // Runs the gap analysis in place, so a saved position joins the plan without leaving this page.
    async function analyse(posting: PostingSummary) {
        setAnalysing(posting.id);
        setAnalyseError(null);
        try {
            await api.post(`/api/postings/${posting.id}/analyse`, {});
            await load();
            setPostingId(posting.id);
        } catch (e) {
            setAnalyseError(message(e));
        } finally {
            setAnalysing(null);
        }
    }

    async function tick(gap: Gap) {
        setBusyId(gap.requirementId);
        setNotice(null);
        try {
            const result = await api.post<Rescan>(`/api/gaps/${gap.requirementId}/done`, {});
            if (result) {
                setGaps((current) => (current ?? []).map((g) => (g.postingId === gap.postingId ? { ...g, postingScore: result.newScore } : g)).map((g) => (g.requirementId === gap.requirementId ? result.gap : g)));
                setNotice({
                    kind: "success",
                    text: `${gap.company} ${result.oldScore}% → ${result.newScore}%. Coverage: ${coverageLabel[result.oldCoverage]} → ${coverageLabel[result.newCoverage]}.`,
                    review: result.newItemsToReview,
                });
            }
        } catch (e) {
            setNotice({ kind: "error", text: message(e) });
        } finally {
            setBusyId(null);
        }
    }

    async function undo(gap: Gap) {
        setBusyId(gap.requirementId);
        setNotice(null);
        try {
            const updated = await api.post<Gap>(`/api/gaps/${gap.requirementId}/undo`, {});
            if (updated) setGaps((current) => (current ?? []).map((g) => (g.requirementId === gap.requirementId ? updated : g)));
        } catch (e) {
            setNotice({ kind: "error", text: message(e) });
        } finally {
            setBusyId(null);
        }
    }

    return (
        <PageFrame full>
            <PageHeader
                title="Gap plan"
                description="What your positions ask that you cannot show yet, biggest impact first. Tick one after you changed the repo and it is re-scanned."
                action={
                    <div role="tablist" aria-label="Show" className="inline-flex gap-1 rounded-lg bg-secondary p-1">
                        {statuses.map((s) => (
                            <button
                                key={s.id}
                                type="button"
                                role="tab"
                                aria-selected={status === s.id}
                                onClick={() => setStatus(s.id)}
                                className={cx(
                                    "cursor-pointer rounded-md px-3 py-1.5 text-sm font-semibold outline-focus-ring transition focus-visible:outline-2",
                                    status === s.id ? "bg-primary text-secondary shadow-xs" : "text-quaternary hover:text-secondary",
                                )}
                            >
                                {s.label}
                            </button>
                        ))}
                    </div>
                }
            />

            {notice && (
                <div
                    role={notice.kind === "error" ? "alert" : "status"}
                    className={`flex items-start justify-between gap-3 rounded-lg px-4 py-2.5 text-sm ring-1 ring-secondary ${notice.kind === "error" ? "bg-error-primary text-error-primary" : "bg-secondary text-primary"}`}
                >
                    <p>
                        <span className="font-semibold">{notice.text}</span>
                        {notice.review ? (
                            <>
                                {" "}
                                {notice.review} new item{notice.review === 1 ? "" : "s"} found in the repo.{" "}
                                <Link to="/settings/profile" className="font-semibold underline">
                                    Review on your profile
                                </Link>
                                .
                            </>
                        ) : null}
                    </p>
                    <button type="button" onClick={() => setNotice(null)} className="shrink-0 cursor-pointer font-semibold underline">
                        Dismiss
                    </button>
                </div>
            )}

            {error ? (
                <div role="alert" className="flex items-center gap-3 text-sm text-error-primary">
                    {error}
                    <Button size="sm" color="secondary" onPress={load}>
                        Try again
                    </Button>
                </div>
            ) : gaps === null || saved === null ? (
                <p className="text-md text-tertiary" aria-busy="true">
                    Loading your gaps...
                </p>
            ) : rail.length === 0 && pending.length === 0 ? (
                <div className="rounded-xl bg-primary px-5 py-10 text-center shadow-xs ring-1 ring-secondary">
                    <p className="text-md font-semibold text-primary">Add a position first.</p>
                    <p className="mt-1 text-sm text-tertiary">What it asks that you cannot show yet becomes your to-do list here.</p>
                    <div className="mt-4">
                        <Button size="md" href="/find">
                            Find positions
                        </Button>
                    </div>
                </div>
            ) : (
                <div className="grid min-h-0 flex-1 items-stretch gap-4 lg:grid-cols-[280px_minmax(0,1fr)]">
                    <aside aria-label="Positions" className="flex min-h-0 flex-col gap-3 overflow-y-auto rounded-xl bg-primary p-2 shadow-xs ring-1 ring-secondary">
                        <button type="button" onClick={() => setPostingId("all")} className={railButton(postingId === "all")}>
                            <span className="text-sm font-semibold text-primary">All positions</span>
                            <span className="text-xs text-tertiary">{totalOpen} open {totalOpen === 1 ? "gap" : "gaps"}</span>
                        </button>
                        {rail.map((r) => (
                            <button key={r.id} type="button" onClick={() => setPostingId(r.id)} className={railButton(postingId === r.id)}>
                                <span className="flex items-center justify-between gap-2">
                                    <span className="truncate text-sm font-semibold text-primary">{r.company}</span>
                                    <span className="shrink-0 text-xs font-semibold text-secondary">{r.score}%</span>
                                </span>
                                <span className="truncate text-xs text-tertiary">{r.title}</span>
                                <span className="text-xs text-tertiary">{r.open} open</span>
                            </button>
                        ))}

                        {pending.length > 0 && (
                            <div className="mt-1 border-t border-secondary pt-3">
                                <p className="px-3 pb-2 text-xs font-semibold text-tertiary">Not in your plan yet</p>
                                <ul className="flex flex-col gap-1">
                                    {pending.map((p) => (
                                        <li key={p.id} className="flex items-center justify-between gap-2 px-3 py-1.5">
                                            <Link to={`/positions/${p.id}`} className="min-w-0 rounded-xs outline-focus-ring hover:underline focus-visible:outline-2">
                                                <span className="block truncate text-sm font-medium text-primary">{p.company}</span>
                                                <span className="block truncate text-xs text-tertiary">{p.title}</span>
                                            </Link>
                                            <Button size="sm" isLoading={analysing === p.id} isDisabled={analysing !== null} onPress={() => analyse(p)}>
                                                Analyse
                                            </Button>
                                        </li>
                                    ))}
                                </ul>
                                {analysing && (
                                    <p role="status" className="px-3 pt-2 text-xs text-tertiary">
                                        Comparing with your profile. This takes up to a minute.
                                    </p>
                                )}
                                {analyseError && (
                                    <p role="alert" className="px-3 pt-2 text-xs text-error-primary">
                                        {analyseError}
                                    </p>
                                )}
                            </div>
                        )}
                    </aside>

                    <section aria-label="Gaps" className="min-h-0 overflow-y-auto rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
                        {visible.length === 0 ? (
                            <p className="px-5 py-10 text-center text-sm text-tertiary">
                                {rail.length === 0
                                    ? "Analyse a position on the left. What it asks that you cannot show yet appears here."
                                    : status === "open"
                                      ? "No open gaps here. Everything this asks, you can already show."
                                      : "Nothing matches."}
                            </p>
                        ) : (
                            groups.map(([name, rows]) => (
                                <div key={name}>
                                    {postingId === "all" && <h2 className="sticky top-0 z-10 border-b border-secondary bg-secondary px-4 py-2 text-xs font-semibold text-secondary">{name}</h2>}
                                    <ul className={itemsInCard}>
                                        {rows.map((g) => (
                                            <li key={g.requirementId} className="flex items-start gap-3 px-4 py-3">
                                                <div className="mt-0.5 flex size-5 shrink-0 items-center justify-center">
                                                    {busyId === g.requirementId ? (
                                                        <span role="status" aria-label="Re-scanning" className="size-4 animate-spin rounded-full border-2 border-secondary border-t-brand-solid" />
                                                    ) : (
                                                        <input
                                                            type="checkbox"
                                                            className="size-4 cursor-pointer accent-brand-600 dark:accent-brand-400"
                                                            checked={g.done}
                                                            disabled={busyId !== null}
                                                            aria-label={g.done ? `Undo: ${g.action ?? g.requirement}` : `Mark done: ${g.action ?? g.requirement}`}
                                                            onChange={() => (g.done ? undo(g) : tick(g))}
                                                        />
                                                    )}
                                                </div>
                                                <div className="min-w-0 flex-1">
                                                    <p className={`text-sm font-medium text-primary ${g.done ? "line-through opacity-70" : ""}`}>{g.action ?? g.requirement}</p>
                                                    <p className="mt-0.5 text-xs text-tertiary">
                                                        {g.requirement}
                                                        {g.project && (
                                                            <>
                                                                {" "}
                                                                · <code className="rounded bg-secondary px-1 py-0.5 font-mono">{g.project.replace(/^new:/, "")}</code>
                                                            </>
                                                        )}
                                                        {postingId !== "all" ? null : (
                                                            <>
                                                                {" "}
                                                                ·{" "}
                                                                <Link to={`/positions/${g.postingId}`} className="font-semibold text-brand-secondary hover:underline">
                                                                    {g.company}
                                                                </Link>
                                                            </>
                                                        )}
                                                        {g.dueDate && ` · by ${formatDate(g.dueDate)}`}
                                                    </p>
                                                    {g.project?.startsWith("new:") && <p className="mt-0.5 text-xs text-tertiary">Add the repo to GitHub and sync your profile to raise your score.</p>}
                                                </div>
                                                <div className="flex shrink-0 flex-wrap justify-end gap-1.5">
                                                    <Badge type="pill-color" size="sm" color={g.importance === "required" ? "brand" : "gray"}>
                                                        {g.importance === "required" ? "Required" : "Nice"}
                                                    </Badge>
                                                    <Badge type="pill-color" size="sm" color={g.coverage === "strong" ? "success" : g.coverage === "partial" ? "warning" : "gray"}>
                                                        {coverageLabel[g.coverage]}
                                                    </Badge>
                                                    {!g.done && g.scoreGain > 0 && (
                                                        <Badge type="pill-color" size="sm" color="gray">
                                                            +{g.scoreGain}%
                                                        </Badge>
                                                    )}
                                                </div>
                                            </li>
                                        ))}
                                    </ul>
                                </div>
                            ))
                        )}
                    </section>
                </div>
            )}
        </PageFrame>
    );
}
