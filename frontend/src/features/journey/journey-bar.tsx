import { useCallback, useEffect, useState } from "react";
import { useLocation } from "react-router";
import { ApiError, api } from "@/core/api";

export type Journey = {
    /** 1 build profile, 2 pick a posting, 3 close gaps, 4 apply and track. */
    currentStep: 1 | 2 | 3 | 4;
    /** True once the best posting is marked applied. */
    complete: boolean;
    hasSources: boolean;
    confirmedItemCount: number;
    savedPostingCount: number;
    analysedPostingCount: number;
    bestPosting: { id: string; company: string; title: string; score: number } | null;
    openGapCount: number;
    documentCountForBest: number;
    bestOnBoard: boolean;
    bestApplied: boolean;
    onBoardCount: number;
    appliedCount: number;
    nextAction: { label: string; href: string };
};

/** Loads GET /api/journey again on every navigation, so the first-start screen follows what the user just did. */
export function useJourney() {
    const { key } = useLocation();
    const [journey, setJourney] = useState<Journey | null>(null);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setError(null);
        try {
            setJourney(await api.get<Journey>("/api/journey"));
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Something went wrong. Try again.");
        }
    }, []);

    useEffect(() => {
        load();
    }, [load, key]);

    return { journey, error, reload: load };
}
