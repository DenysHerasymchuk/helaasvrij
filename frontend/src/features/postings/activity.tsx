import { useEffect, useState } from "react";
import { api } from "@/core/api";
import { stageLabel } from "@/features/applications/types";
import { type ApplicationEvent } from "@/features/sprints/types";
import { errorMessage } from "@/features/sprints/use-sprints";
import { formatDateTime } from "./types";

function describe(e: ApplicationEvent): string {
    switch (e.type) {
        case "added":
            return e.sprintName ? `Added to ${e.sprintName}` : "Added to the backlog";
        case "stageChanged":
            return `Moved from ${e.fromStage ? stageLabel(e.fromStage) : "nowhere"} to ${e.toStage ? stageLabel(e.toStage) : "nowhere"}`;
        default:
            return e.sprintName ? `Moved into ${e.sprintName}` : "Moved back to the backlog";
    }
}

/** A position's history, newest first. `version` changes when the card moves, so the list reloads. */
export function ActivityPanel({ applicationId, version }: { applicationId: string; version: string }) {
    const [events, setEvents] = useState<ApplicationEvent[] | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        let alive = true;
        api.get<ApplicationEvent[]>(`/api/applications/${applicationId}/events`)
            .then((list) => alive && setEvents([...(list ?? [])].reverse()))
            .catch((e) => alive && setError(errorMessage(e)));
        return () => {
            alive = false;
        };
    }, [applicationId, version]);

    return (
        <section className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
            <h2 className="border-b border-secondary px-5 py-4 text-md font-semibold text-primary">Activity</h2>
            {error ? (
                <p role="alert" className="px-5 py-4 text-sm text-error-primary">
                    {error}
                </p>
            ) : events === null ? (
                <p role="status" className="px-5 py-4 text-sm text-tertiary">
                    Loading activity...
                </p>
            ) : (
                <ol className="flex flex-col gap-3 px-5 py-4">
                    {events.map((e, i) => (
                        <li key={i} className="flex flex-col">
                            <span className="text-sm text-primary">{describe(e)}</span>
                            <time dateTime={e.at} className="text-xs text-tertiary">
                                {formatDateTime(e.at)}
                            </time>
                        </li>
                    ))}
                </ol>
            )}
        </section>
    );
}
