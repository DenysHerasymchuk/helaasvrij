import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { type Coverage, formatDate } from "@/features/postings/types";

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

const coverageLabel: Record<Coverage, string> = { strong: "Strong", partial: "Partial", none: "Not shown" };

export function GapsPage() {
    const [gaps, setGaps] = useState<Gap[] | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [busyId, setBusyId] = useState<string | null>(null);
    const [notice, setNotice] = useState<Notice | null>(null);
    const [status, setStatus] = useState<"open" | "done" | "all">("open");
    const [postingId, setPostingId] = useState("all");
    const [groupBy, setGroupBy] = useState<"posting" | "repo">("posting");

    const load = useCallback(async () => {
        setError(null);
        try {
            setGaps((await api.get<Gap[]>("/api/gaps")) ?? []);
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Something went wrong. Try again.");
        }
    }, []);

    useEffect(() => {
        void load();
    }, [load]);

    const postings = useMemo(() => {
        const map = new Map<string, string>();
        for (const g of gaps ?? []) map.set(g.postingId, g.company);
        return [...map.entries()];
    }, [gaps]);

    const visible = useMemo(
        () => (gaps ?? []).filter((g) => (status === "all" || (status === "done") === g.done) && (postingId === "all" || g.postingId === postingId)),
        [gaps, status, postingId],
    );

    const groups = useMemo(() => {
        const map = new Map<string, Gap[]>();
        for (const g of visible) {
            const key = groupBy === "posting" ? `${g.company}: ${g.postingTitle}` : (g.project ?? "No repo");
            map.set(key, [...(map.get(key) ?? []), g]);
        }
        return [...map.entries()];
    }, [visible, groupBy]);

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
            setNotice({ kind: "error", text: e instanceof ApiError ? e.message : "Something went wrong. Try again." });
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
            setNotice({ kind: "error", text: e instanceof ApiError ? e.message : "Something went wrong. Try again." });
        } finally {
            setBusyId(null);
        }
    }

    const selectClass = "rounded-lg bg-primary px-3 py-2 text-sm text-primary shadow-xs ring-1 ring-primary ring-inset";

    return (
        <div className="mx-auto flex max-w-4xl flex-col gap-6 px-4 py-8 md:px-8">
            <div>
                <h1 className="text-display-xs font-semibold text-primary">Gap plan</h1>
                <p className="mt-1 text-md text-tertiary">Every gap from your postings in one checklist, biggest impact first. Tick one after you changed the repo and it is re-scanned.</p>
            </div>

            {notice && (
                <div
                    role={notice.kind === "error" ? "alert" : "status"}
                    className={`flex items-start justify-between gap-3 rounded-lg px-4 py-3 text-sm ring-1 ring-secondary ${notice.kind === "error" ? "bg-error-primary text-error-primary" : "bg-secondary text-primary"}`}
                >
                    <div>
                        <p className="font-semibold">{notice.text}</p>
                        {notice.review ? (
                            <p className="mt-1 text-secondary">
                                {notice.review} new item{notice.review === 1 ? "" : "s"} found in the repo.{" "}
                                <Link to="/profile" className="font-semibold underline">
                                    Review on your profile
                                </Link>{" "}
                                (only confirmed items count).
                            </p>
                        ) : null}
                    </div>
                    <button type="button" onClick={() => setNotice(null)} className="shrink-0 font-semibold underline">
                        Dismiss
                    </button>
                </div>
            )}

            <div className="flex flex-wrap items-center gap-3">
                <label className="flex items-center gap-2 text-sm text-secondary">
                    Show
                    <select className={selectClass} value={status} onChange={(e) => setStatus(e.target.value as typeof status)}>
                        <option value="open">Open</option>
                        <option value="done">Done</option>
                        <option value="all">All</option>
                    </select>
                </label>
                <label className="flex items-center gap-2 text-sm text-secondary">
                    Posting
                    <select className={selectClass} value={postingId} onChange={(e) => setPostingId(e.target.value)}>
                        <option value="all">All postings</option>
                        {postings.map(([id, company]) => (
                            <option key={id} value={id}>
                                {company}
                            </option>
                        ))}
                    </select>
                </label>
                <label className="flex items-center gap-2 text-sm text-secondary">
                    Group by
                    <select className={selectClass} value={groupBy} onChange={(e) => setGroupBy(e.target.value as typeof groupBy)}>
                        <option value="posting">Posting</option>
                        <option value="repo">Repo</option>
                    </select>
                </label>
            </div>

            {error ? (
                <div role="alert" className="flex items-center gap-3 text-sm text-error-primary">
                    {error}
                    <Button size="sm" color="secondary" onPress={load}>
                        Try again
                    </Button>
                </div>
            ) : gaps === null ? (
                <p className="text-md text-tertiary" aria-busy="true">
                    Loading your gaps…
                </p>
            ) : gaps.length === 0 ? (
                <div className="rounded-xl bg-primary px-5 py-10 text-center shadow-xs ring-1 ring-secondary">
                    <p className="text-md font-semibold text-primary">No gaps.</p>
                    <p className="mt-1 text-sm text-tertiary">Add a posting and run the analysis.</p>
                    <div className="mt-4">
                        <Button size="md" href="/postings">
                            Go to postings
                        </Button>
                    </div>
                </div>
            ) : visible.length === 0 ? (
                <p className="text-md text-tertiary">Nothing matches these filters.</p>
            ) : (
                groups.map(([name, rows]) => (
                    <section key={name} className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
                        <h2 className="border-b border-secondary px-5 py-3 text-sm font-semibold text-primary">{name}</h2>
                        <ul className="divide-y divide-secondary">
                            {rows.map((g) => (
                                <li key={g.requirementId} className="flex items-start gap-3 px-5 py-4">
                                    <div className="mt-0.5 flex size-5 shrink-0 items-center justify-center">
                                        {busyId === g.requirementId ? (
                                            <span role="status" aria-label="Re-scanning" className="size-4 animate-spin rounded-full border-2 border-secondary border-t-brand-solid" />
                                        ) : (
                                            <input
                                                type="checkbox"
                                                className="size-4 cursor-pointer accent-current"
                                                checked={g.done}
                                                disabled={busyId !== null}
                                                aria-label={g.done ? `Undo: ${g.action ?? g.requirement}` : `Mark done: ${g.action ?? g.requirement}`}
                                                onChange={() => (g.done ? undo(g) : tick(g))}
                                            />
                                        )}
                                    </div>
                                    <div className="flex min-w-0 flex-1 flex-col gap-1.5">
                                        <p className={`text-md font-medium text-primary ${g.done ? "line-through opacity-70" : ""}`}>{g.action ?? g.requirement}</p>
                                        <p className="text-sm text-tertiary">
                                            {g.requirement}
                                            {g.project && (
                                                <>
                                                    {" "}
                                                    · <code className="rounded bg-secondary px-1.5 py-0.5 font-mono text-xs">{g.project.replace(/^new:/, "")}</code>
                                                </>
                                            )}
                                            {" "}
                                            ·{" "}
                                            <Link to={`/postings/${g.postingId}`} className="font-semibold text-brand-secondary hover:underline">
                                                {g.company}
                                            </Link>
                                            {g.dueDate && ` · by ${formatDate(g.dueDate)}`}
                                        </p>
                                        {g.project?.startsWith("new:") && <p className="text-sm text-tertiary">Add the repo to GitHub and sync your profile to raise your score.</p>}
                                        <div className="flex flex-wrap gap-2">
                                            <Badge type="pill-color" size="sm" color={g.importance === "required" ? "brand" : "gray"}>
                                                {g.importance === "required" ? "Required" : "Nice to have"}
                                            </Badge>
                                            <Badge type="pill-color" size="sm" color={g.coverage === "strong" ? "success" : g.coverage === "partial" ? "warning" : "gray"}>
                                                {coverageLabel[g.coverage]}
                                            </Badge>
                                            {!g.done && g.scoreGain > 0 && (
                                                <Badge type="pill-color" size="sm" color="gray">
                                                    Up to +{g.scoreGain}% ({g.company} {g.postingScore}%)
                                                </Badge>
                                            )}
                                        </div>
                                    </div>
                                </li>
                            ))}
                        </ul>
                    </section>
                ))
            )}
        </div>
    );
}
