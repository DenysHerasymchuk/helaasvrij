import { useCallback, useEffect, useState } from "react";
import { ApiError, api } from "@/core/api";
import type { Sprint } from "./types";

export const errorMessage = (e: unknown) => (e instanceof ApiError ? e.message : "Something went wrong. Try again.");

/** All sprints (running first, then finished) and the running one. `sprints` is null while loading. */
export function useSprints() {
    const [sprints, setSprints] = useState<Sprint[] | null>(null);
    const [error, setError] = useState<string | null>(null);

    const reload = useCallback(async () => {
        setError(null);
        try {
            setSprints((await api.get<Sprint[]>("/api/sprints")) ?? []);
        } catch (e) {
            setError(errorMessage(e));
        }
    }, []);

    useEffect(() => {
        reload();
    }, [reload]);

    return { sprints, active: sprints?.find((s) => s.status === "active") ?? null, error, reload };
}
