import { Link } from "react-router";
import { cardClass } from "@/core/ui";
import type { Home } from "./use-home";

function Tile({ label, value, hint, to }: { label: string; value: string | number; hint?: string; to: string }) {
    return (
        <Link to={to} className={`${cardClass} flex flex-col gap-0.5 px-4 py-3 outline-focus-ring transition hover:bg-primary_hover focus-visible:outline-2`}>
            <span className="text-sm text-tertiary">{label}</span>
            <span className="font-display text-display-xs font-semibold text-primary">{value}</span>
            {hint && <span className="truncate text-xs text-quaternary">{hint}</span>}
        </Link>
    );
}

/** Five numbers that say where the search stands. Each opens the page behind it. */
export function KpiRow({ home }: { home: Home }) {
    const inSprint = home.applications.filter((a) => home.activeSprintId && a.sprintId === home.activeSprintId);
    const best = home.postings.filter((p) => p.analysed && p.score != null).sort((a, b) => b.score! - a.score!)[0];

    return (
        <section aria-label="Numbers" className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-5">
            <Tile label="Positions" value={home.postings.length} hint="In your list" to="/list" />
            <Tile label="In the sprint" value={home.activeSprintId ? inSprint.length : 0} hint={home.activeSprintId ? "On the board" : "No sprint running"} to={home.activeSprintId ? "/board" : "/backlog"} />
            <Tile label="Applied" value={inSprint.filter((a) => a.stage !== "saved").length} hint="This sprint" to="/board" />
            <Tile label="Interviews" value={inSprint.filter((a) => a.stage === "interview").length} hint="Open now" to="/board" />
            <Tile label="Best match" value={best ? `${best.score}%` : "None yet"} hint={best ? best.company : "Check a position"} to={best ? `/positions/${best.id}` : "/gaps"} />
        </section>
    );
}
