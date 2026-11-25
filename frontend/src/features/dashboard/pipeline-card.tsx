import { Link } from "react-router";
import { Button } from "@/components/base/buttons/button";
import { type Stage, stages } from "@/features/applications/types";
import { metaClass } from "@/core/ui";
import { Panel } from "./panel";
import type { Home } from "./use-home";

const bar: Record<Stage, string> = {
    saved: "bg-fg-quaternary",
    applied: "bg-fg-brand-primary_alt",
    interview: "bg-fg-warning-primary",
    offer: "bg-fg-success-primary",
    rejected: "bg-fg-error-primary",
};

/** The running sprint's cards by stage, in one bar. Opens the board. */
export function PipelineCard({ home }: { home: Home }) {
    const cards = home.applications.filter((a) => home.activeSprintId && a.sprintId === home.activeSprintId);
    const counts = stages.map((s) => ({ ...s, n: cards.filter((a) => a.stage === s.id).length }));

    return (
        <Panel title="Pipeline" description="Where the positions in your sprint stand." order={2}>
            {!home.activeSprintId || cards.length === 0 ? (
                <div className="flex flex-col items-start gap-3 px-5 py-4">
                    <p className={metaClass}>{home.activeSprintId ? "Nothing in the sprint yet." : "No sprint is running."}</p>
                    <Button size="sm" color="secondary" href="/backlog">
                        {home.activeSprintId ? "Add positions" : "Start a sprint"}
                    </Button>
                </div>
            ) : (
                <Link to="/board" aria-label="Open the board" className="flex flex-col gap-3 px-5 py-4 outline-focus-ring focus-visible:outline-2 focus-visible:-outline-offset-2">
                    <div className="flex h-3 overflow-hidden rounded-full bg-secondary">
                        {counts.map((c) => c.n > 0 && <span key={c.id} className={bar[c.id]} style={{ width: `${(c.n / cards.length) * 100}%` }} />)}
                    </div>
                    <ul className="grid grid-cols-5 gap-1 text-center">
                        {counts.map((c) => (
                            <li key={c.id}>
                                <span className="block text-md font-semibold text-primary">{c.n}</span>
                                <span className="block truncate text-xs text-tertiary">{c.label}</span>
                            </li>
                        ))}
                    </ul>
                </Link>
            )}
        </Panel>
    );
}
