import { useCallback, useEffect, useState } from "react";
import { api } from "@/core/api";
import type { Application, Stage } from "@/features/applications/types";
import { type Journey, useJourney } from "@/features/journey/journey-bar";
import type { PostingSummary } from "@/features/postings/types";
import { type Profile, getProfile } from "@/features/profile/api";
import { errorMessage, useSprints } from "@/features/sprints/use-sprints";
import { type DashboardData, useDashboard } from "./use-dashboard";

export type Activity = {
    type: "added" | "stageChanged" | "sprintChanged";
    fromStage: Stage | null;
    toStage: Stage | null;
    postingId: string;
    key: string;
    title: string;
    company: string;
    sprintName: string | null;
    at: string;
};

export type Home = {
    dashboard: DashboardData;
    postings: PostingSummary[];
    applications: Application[];
    activity: Activity[];
    profile: Profile | null;
    journey: Journey | null;
    activeSprintId: string | null;
};

/** Everything Home shows, loaded once. `home` is null while loading; `error` holds the first failure. */
export function useHome() {
    const dash = useDashboard();
    const { active, sprints, reload: reloadSprints } = useSprints();
    const journeyState = useJourney();
    const journey = journeyState.journey;
    const [extra, setExtra] = useState<{ postings: PostingSummary[]; applications: Application[]; activity: Activity[]; profile: Profile | null } | null>(null);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setError(null);
        try {
            const [postings, applications, activity, profile] = await Promise.all([
                api.get<PostingSummary[]>("/api/postings"),
                api.get<Application[]>("/api/applications"),
                api.get<Activity[]>("/api/applications/activity?take=8"),
                getProfile().catch(() => null),
            ]);
            setExtra({ postings: postings ?? [], applications: applications ?? [], activity: activity ?? [], profile });
        } catch (e) {
            setError(errorMessage(e));
        }
    }, []);

    useEffect(() => {
        void load();
    }, [load]);

    const home: Home | null =
        dash.data && extra && sprints
            ? { dashboard: dash.data, ...extra, journey, activeSprintId: active?.id ?? null }
            : null;

    return {
        home,
        journey: journeyState,
        error: dash.error ?? error,
        reload: () => {
            dash.reload();
            reloadSprints();
            void load();
        },
    };
}
