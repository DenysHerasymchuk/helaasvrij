import { useState } from "react";
import { Link } from "react-router";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { ScoreRing } from "@/features/dashboard/score-ring";
import { type Coverage, type PostingDetail, type PostingRequirement, formatDate } from "./types";

const coverageBadge: Record<Coverage, { label: string; color: "success" | "warning" | "gray" }> = {
    strong: { label: "Strong", color: "success" },
    partial: { label: "Partial", color: "warning" },
    none: { label: "Not shown", color: "gray" },
};

function safeHref(url: string | null) {
    return url && /^https?:\/\//i.test(url) ? url : null;
}

export function GapPanel({ posting, onUpdated }: { posting: PostingDetail; onUpdated: (posting: PostingDetail) => void }) {
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const { analysis } = posting;
    const missing = posting.requirements.filter((r) => r.coverage === "none").length;

    async function run() {
        setBusy(true);
        setError(null);
        try {
            const updated = await api.post<PostingDetail>(`/api/postings/${posting.id}/analyse`, {});
            if (updated) onUpdated(updated);
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Something went wrong. Try again.");
        } finally {
            setBusy(false);
        }
    }

    return (
        <section className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
            <h2 className="border-b border-secondary px-5 py-4 text-md font-semibold text-primary">Gap analysis</h2>
            <div className="flex flex-col gap-4 px-5 py-4">
                {analysis.analysed ? (
                    <>
                        <div className="flex items-center gap-4">
                            <ScoreRing value={analysis.score} size={72} />
                            <div>
                                <p className="text-md font-semibold text-primary">{analysis.score}% match</p>
                                {analysis.analysedAt && <p className="text-sm text-tertiary">Analysed {formatDate(analysis.analysedAt)}</p>}
                            </div>
                        </div>
                        <p className="text-sm text-secondary">
                            Score = sum of (weight × coverage) ÷ sum of weights. Required counts 2, nice to have counts 1. Strong counts 1, partial 0.5, none 0.
                            Code computes it from the labels below, so the same data always gives the same number.
                        </p>
                        {analysis.fromDemoCache && (
                            <p role="status" className="rounded-lg bg-secondary px-4 py-3 text-sm text-secondary">
                                The AI service was slow or unavailable, so this shows the last saved analysis of this demo posting.
                            </p>
                        )}
                        {analysis.noClearFit && (
                            <div role="status" className="rounded-lg bg-secondary px-4 py-3 text-sm text-secondary">
                                <p className="font-semibold text-primary">No clear fit yet</p>
                                <p className="mt-1">
                                    Your confirmed profile covers too little of what this posting requires.
                                    {missing > 0 && ` ${missing} requirement${missing === 1 ? "" : "s"} show no evidence.`} Add or confirm items on your profile, or close the
                                    gaps below, then analyse again.
                                </p>
                            </div>
                        )}
                    </>
                ) : (
                    <p className="text-md text-tertiary">
                        Compare this posting with the items you confirmed on your profile. You get a match score and one action per gap.
                    </p>
                )}
                {error && (
                    <p role="alert" className="text-sm text-error-primary">
                        {error}{" "}
                        {error.includes("Confirm at least one") && (
                            <Link to="/profile" className="font-semibold underline">
                                Open profile
                            </Link>
                        )}
                    </p>
                )}
                <div>
                    <Button size="md" onPress={run} isLoading={busy}>
                        {analysis.analysed ? "Re-analyse" : "Analyse"}
                    </Button>
                </div>
            </div>
        </section>
    );
}

export function GapMatrix({ posting }: { posting: PostingDetail }) {
    return (
        <section className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
            <h2 className="border-b border-secondary px-5 py-4 text-md font-semibold text-primary">Gap matrix</h2>
            <ul className="divide-y divide-secondary">
                {posting.requirements.map((r) => (
                    <GapRow key={r.id} requirement={r} />
                ))}
            </ul>
        </section>
    );
}

function GapRow({ requirement: r }: { requirement: PostingRequirement }) {
    const badge = coverageBadge[r.coverage];
    const href = safeHref(r.evidenceSource?.url ?? null);
    return (
        <li className="flex flex-col gap-3 px-5 py-4">
            <div className="flex flex-wrap items-center gap-2">
                <p className="min-w-0 flex-1 text-md font-medium text-primary">{r.text}</p>
                <Badge type="pill-color" size="sm" color={r.importance === "required" ? "brand" : "gray"}>
                    {r.importance === "required" ? "Required" : "Nice to have"}
                </Badge>
                <Badge type="pill-color" size="sm" color={badge.color}>
                    {badge.label}
                </Badge>
            </div>
            {r.evidenceQuote && (
                <blockquote className="border-l-2 border-secondary pl-3 text-sm text-secondary">
                    {/* Quotes come from the student's own sources: plain text only. */}
                    <p>“{r.evidenceQuote}”</p>
                    {r.evidenceSource &&
                        (href ? (
                            <a href={href} target="_blank" rel="noopener noreferrer" className="mt-1 inline-block font-semibold text-brand-secondary hover:underline">
                                {r.evidenceSource.path}
                            </a>
                        ) : (
                            <Link to="/profile" className="mt-1 inline-block font-semibold text-brand-secondary hover:underline">
                                {r.evidenceSource.label}: {r.evidenceSource.path}
                            </Link>
                        ))}
                </blockquote>
            )}
            {r.action && (
                <p className="text-sm text-primary">
                    <span className="font-semibold">Next step</span> in <code className="rounded bg-secondary px-1.5 py-0.5 font-mono text-xs">{r.actionProject?.replace(/^new:/, "")}</code>: {r.action}
                    {r.dueDate && <span className="text-tertiary"> · by {formatDate(r.dueDate)}</span>}
                    {r.actionProject?.startsWith("new:") && <span className="block text-tertiary">Add the repo to GitHub and sync your profile to raise your score.</span>}
                </p>
            )}
            {!r.action && r.coverage !== "strong" && <p className="text-sm text-tertiary">No action suggested. Connect your GitHub so actions can name one of your projects.</p>}
        </li>
    );
}
