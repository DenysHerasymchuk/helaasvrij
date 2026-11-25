import type { ReactNode } from "react";
import { formatDate } from "@/features/postings/types";
import { cardClass } from "@/core/ui";
import { type Sprint, daysLeft, daysLeftLabel, sprintProgress } from "./types";

/** The running sprint at a glance: the goal is the headline, a bar shows how many positions left Saved. */
export function SprintBand({ sprint, action }: { sprint: Sprint; action?: ReactNode }) {
    const progress = sprintProgress(sprint);
    const late = daysLeft(sprint) < 0;
    return (
        <section aria-label={`Running sprint ${sprint.name}`} className={`${cardClass} flex flex-col gap-4 p-5 md:p-6`}>
            <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0">
                    <p className="text-sm font-semibold text-brand-secondary">{sprint.name}</p>
                    <h2 className="mt-1 font-display text-xl font-semibold tracking-tight text-balance text-primary md:text-display-xs">
                        {sprint.goal || "No goal set"}
                    </h2>
                    <p className="mt-1 text-sm text-tertiary">
                        {formatDate(sprint.startsOn)} to {formatDate(sprint.endsOn)}
                        {" - "}
                        <span className={late ? "font-semibold text-error-primary" : undefined}>{daysLeftLabel(sprint)}</span>
                    </p>
                </div>
                {action && <div className="flex shrink-0 flex-wrap gap-2">{action}</div>}
            </div>
            <div className="flex flex-col gap-2">
                <div
                    role="progressbar"
                    aria-label="Positions applied"
                    aria-valuemin={0}
                    aria-valuemax={100}
                    aria-valuenow={progress}
                    className="h-2 overflow-hidden rounded-full bg-secondary"
                >
                    <div className="h-full rounded-full bg-fg-brand-primary_alt transition-all" style={{ width: `${progress}%` }} />
                </div>
                <p className="text-sm text-tertiary">
                    {sprint.applied} of {sprint.items} {sprint.items === 1 ? "position" : "positions"} applied
                    {sprint.interviews > 0 && `, ${sprint.interviews} in interviews`}
                    {sprint.offers > 0 && `, ${sprint.offers} ${sprint.offers === 1 ? "offer" : "offers"}`}
                </p>
            </div>
        </section>
    );
}
