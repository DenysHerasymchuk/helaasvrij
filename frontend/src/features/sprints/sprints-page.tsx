import { useState } from "react";
import { Link } from "react-router";
import { Plus } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { ErrorState, LoadingState, PageHeader, cardClass } from "@/core/ui";
import { formatDate } from "@/features/postings/types";
import { StartSprintModal } from "./start-sprint-modal";
import { type Sprint, daysLeftLabel } from "./types";
import { useSprints } from "./use-sprints";

export function SprintRow({ sprint }: { sprint: Sprint }) {
    const running = sprint.status === "active";
    return (
        <li>
            <Link
                to={`/sprints/${sprint.id}`}
                className="flex flex-col gap-2 px-5 py-4 outline-focus-ring transition hover:bg-primary_hover focus-visible:outline-2 md:flex-row md:items-center md:gap-6"
            >
                <div className="min-w-0 flex-1">
                    <p className="flex flex-wrap items-center gap-2 text-md font-semibold text-primary">
                        {sprint.name}
                        <Badge type="pill-color" size="sm" color={running ? "brand" : "gray"}>
                            {running ? "Running" : "Completed"}
                        </Badge>
                    </p>
                    <p className="truncate text-sm text-tertiary">{sprint.goal || "No goal set"}</p>
                </div>
                <p className="text-sm text-tertiary md:w-56 md:text-right">
                    {formatDate(sprint.startsOn)} to {formatDate(sprint.endsOn)}
                    {running && <span className="block font-semibold text-secondary">{daysLeftLabel(sprint)}</span>}
                </p>
                <p className="text-sm text-secondary md:w-72 md:text-right">
                    {sprint.items} {sprint.items === 1 ? "position" : "positions"}, {sprint.applied} applied, {sprint.interviews} interviews, {sprint.offers} offers
                </p>
            </Link>
        </li>
    );
}

/** History of every sprint, the running one first. */
export function SprintsPage() {
    const { sprints, active, error, reload } = useSprints();
    const [starting, setStarting] = useState(false);

    return (
        <div className="mx-auto flex w-full max-w-6xl flex-col gap-6 px-4 py-8 md:px-8">
            <PageHeader
                title="Sprints"
                description="Each sprint is one round of applications. Open one to see what it produced."
                action={
                    !active && (
                        <Button size="md" iconLeading={Plus} onPress={() => setStarting(true)}>
                            Start a sprint
                        </Button>
                    )
                }
            />
            {error ? (
                <ErrorState message={error} onRetry={reload} />
            ) : sprints === null ? (
                <LoadingState>Loading sprints...</LoadingState>
            ) : sprints.length === 0 ? (
                <section className={`${cardClass} flex flex-col items-start gap-3 p-6`}>
                    <p className="text-md font-semibold text-primary">No sprints yet.</p>
                    <p className="text-md text-tertiary">Start your first sprint to track a round of applications and look back on it later.</p>
                    <Button size="md" onPress={() => setStarting(true)}>
                        Start a sprint
                    </Button>
                </section>
            ) : (
                <ul className={`${cardClass} flex flex-col divide-y divide-secondary overflow-hidden`}>
                    {sprints.map((s) => (
                        <SprintRow key={s.id} sprint={s} />
                    ))}
                </ul>
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
        </div>
    );
}
