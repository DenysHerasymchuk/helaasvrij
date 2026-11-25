import { Link } from "react-router";
import { ErrorState, LoadingState, cardClass, itemsInCard } from "@/core/ui";
import { stages } from "@/features/applications/types";
import { formatDate } from "@/features/postings/types";
import { type Position, useProject } from "./use-project";

function Tile({ label, value }: { label: string; value: number }) {
    return (
        <div className={`${cardClass} flex flex-col gap-1 p-4`}>
            <p className="text-sm text-tertiary">{label}</p>
            <p className="font-display text-display-xs font-semibold text-primary">{value}</p>
        </div>
    );
}

function Card({ title, children }: { title: string; children: React.ReactNode }) {
    return (
        <section className={cardClass}>
            <h2 className="border-b border-secondary px-5 py-4 text-md font-semibold text-primary">{title}</h2>
            {children}
        </section>
    );
}

const rowClass = "flex items-center justify-between gap-3 px-5 py-3 outline-focus-ring hover:bg-primary_hover focus-visible:outline-2 focus-visible:-outline-offset-2";

function PositionLink({ p, trailing }: { p: Position; trailing: string }) {
    return (
        <li>
            <Link to={`/positions/${p.id}`} className={rowClass}>
                <span className="min-w-0">
                    <span className="block truncate text-sm font-semibold text-primary">{p.title}</span>
                    <span className="block truncate text-sm text-tertiary">
                        {p.key} · {p.company}
                    </span>
                </span>
                <span className="shrink-0 text-sm text-secondary">{trailing}</span>
            </Link>
        </li>
    );
}

/** The whole search at a glance: running sprint, counts, where positions stand, what is due, best matches. */
export function SummaryPage() {
    const { positions, error, reload } = useProject();
    if (error) return <ErrorState message={error} onRetry={reload} />;
    if (!positions) return <LoadingState>Loading the summary...</LoadingState>;

    const inBacklog = positions.filter((p) => !p.application || !p.application.sprintId).length;
    const counts = [
        { id: "backlog", label: "Backlog", color: "bg-fg-quaternary", n: inBacklog },
        ...stages.map((s) => ({ id: s.id, label: s.label, color: s.id === "offer" ? "bg-fg-success-primary" : s.id === "rejected" ? "bg-fg-error-primary" : "bg-fg-brand-primary_alt", n: positions.filter((p) => p.application?.sprintId && p.application.stage === s.id).length })),
    ];
    const max = Math.max(1, ...counts.map((c) => c.n));
    const today = new Date().toISOString().slice(0, 10);
    const due = positions.filter((p) => p.deadline && p.deadline >= today && p.application?.stage !== "rejected").sort((a, b) => a.deadline!.localeCompare(b.deadline!)).slice(0, 5);
    const best = positions.filter((p) => p.analysed && p.score != null).sort((a, b) => b.score! - a.score!).slice(0, 5);

    return (
        <div className="flex flex-col gap-6">
            <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
                <Tile label="Positions" value={positions.length} />
                <Tile label="In the backlog" value={inBacklog} />
                <Tile label="In the sprint" value={positions.length - inBacklog} />
                <Tile label="Applied or further" value={positions.filter((p) => p.application && p.application.stage !== "saved").length} />
            </div>

            <div className="grid gap-6 lg:grid-cols-2">
                <Card title="Status overview">
                    <ul className="flex flex-col gap-3 px-5 py-4">
                        {counts.map((c) => (
                            <li key={c.id} className="grid grid-cols-[6rem_1fr_2rem] items-center gap-3 text-sm">
                                <span className="text-secondary">{c.label}</span>
                                <span className="h-2 overflow-hidden rounded-full bg-secondary">
                                    <span className={`block h-full rounded-full ${c.color}`} style={{ width: `${(c.n / max) * 100}%` }} />
                                </span>
                                <span className="text-right font-semibold text-primary">{c.n}</span>
                            </li>
                        ))}
                    </ul>
                </Card>

                <Card title="Coming up">
                    {due.length === 0 ? (
                        <p className="px-5 py-4 text-sm text-tertiary">No deadlines ahead. Add one on a position.</p>
                    ) : (
                        <ul className={itemsInCard}>
                            {due.map((p) => (
                                <PositionLink key={p.id} p={p} trailing={formatDate(p.deadline!)} />
                            ))}
                        </ul>
                    )}
                </Card>

                <Card title="Best matches">
                    {best.length === 0 ? (
                        <p className="px-5 py-4 text-sm text-tertiary">Check a position against your profile to see its match here.</p>
                    ) : (
                        <ul className={itemsInCard}>
                            {best.map((p) => (
                                <PositionLink key={p.id} p={p} trailing={`${p.score}%`} />
                            ))}
                        </ul>
                    )}
                </Card>
            </div>
        </div>
    );
}
