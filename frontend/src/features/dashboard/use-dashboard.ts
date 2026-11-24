import { useCallback, useEffect, useState } from "react";
import { ApiError, api } from "@/core/api";

export type Importance = "required" | "nice";
export type Coverage = "strong" | "partial" | "none";

export type DashboardSource = { kind: "GitHub" | "CV" | "Site"; label: string; synced: string | null };

export type DashboardRequirement = {
    id: string;
    text: string;
    importance: Importance;
    coverage: Coverage;
    done: boolean;
    action: string | null;
    project: string | null;
    due: string | null;
    /** Match points the posting gains when this requirement becomes strong. */
    gain: number;
};

export type DashboardPosting = {
    id: string;
    company: string;
    title: string;
    city: string;
    language: "NL" | "FR" | "EN";
    deadline: string | null;
    interviewAt: string | null;
    /** 0..100, computed by the API. */
    score: number;
    requirements: DashboardRequirement[];
};

export type ApplicationMonth = { month: string; applied: number; interview: number; offer: number };

export type DashboardData = {
    sources: DashboardSource[];
    postings: DashboardPosting[];
    applicationsByMonth: ApplicationMonth[];
};

export type DashboardState = { data: DashboardData | null; error: string | null; reload: () => void };

/** Loads GET /api/dashboard. `data` is null while loading or after an error. */
export function useDashboard(): DashboardState {
    const [data, setData] = useState<DashboardData | null>(null);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setError(null);
        setData(null);
        try {
            setData(await api.get<DashboardData>("/api/dashboard"));
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Something went wrong. Try again.");
        }
    }, []);

    useEffect(() => {
        load();
    }, [load]);

    return { data, error, reload: load };
}
