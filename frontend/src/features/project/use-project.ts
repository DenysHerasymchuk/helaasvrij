import { useCallback, useEffect, useMemo, useState } from "react";
import { api } from "@/core/api";
import type { Application } from "@/features/applications/types";
import type { PostingSummary } from "@/features/postings/types";
import type { Sprint } from "@/features/sprints/types";
import { errorMessage } from "@/features/sprints/use-sprints";

/** One position with where it stands: no application means it sits in the backlog. */
export type Position = PostingSummary & { application: Application | null; sprint: Sprint | null };

/** Every position joined with its card and sprint. `positions` is null while loading. */
export function useProject() {
    const [postings, setPostings] = useState<PostingSummary[] | null>(null);
    const [applications, setApplications] = useState<Application[]>([]);
    const [sprints, setSprints] = useState<Sprint[]>([]);
    const [error, setError] = useState<string | null>(null);

    const reload = useCallback(async () => {
        setError(null);
        try {
            const [p, a, s] = await Promise.all([
                api.get<PostingSummary[]>("/api/postings"),
                api.get<Application[]>("/api/applications"),
                api.get<Sprint[]>("/api/sprints"),
            ]);
            setApplications(a ?? []);
            setSprints(s ?? []);
            setPostings(p ?? []);
        } catch (e) {
            setError(errorMessage(e));
        }
    }, []);

    useEffect(() => {
        reload();
    }, [reload]);

    const positions = useMemo<Position[] | null>(() => {
        if (!postings) return null;
        const cards = new Map(applications.map((a) => [a.postingId, a]));
        const bySprint = new Map(sprints.map((s) => [s.id, s]));
        return postings.map((p) => {
            const application = cards.get(p.id) ?? null;
            return { ...p, application, sprint: (application?.sprintId && bySprint.get(application.sprintId)) || null };
        });
    }, [postings, applications, sprints]);

    return { positions, sprints, active: sprints.find((s) => s.status === "active") ?? null, error, reload };
}
