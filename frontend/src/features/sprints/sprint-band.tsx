import type { ReactNode } from "react";
import type { Sprint } from "./types";

/** The running sprint, kept small: its name, its goal as the headline, and the actions that belong to it. */
export function SprintBand({ sprint, action }: { sprint: Sprint; action?: ReactNode }) {
    return (
        <section aria-label={`Running sprint ${sprint.name}`} className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
            <div className="min-w-0">
                <p className="text-sm font-semibold text-brand-secondary">{sprint.name}</p>
                <h2 className="font-display text-xl font-semibold tracking-tight text-balance text-primary md:text-display-xs">{sprint.goal || "No goal set"}</h2>
            </div>
            {action && <div className="flex shrink-0 flex-wrap gap-2">{action}</div>}
        </section>
    );
}
