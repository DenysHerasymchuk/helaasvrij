import { useCallback, useEffect, useState } from "react";
import { api } from "@/core/api";
import type { Application } from "@/features/applications/types";
import type { PostingSummary } from "@/features/postings/types";
import { errorMessage } from "@/features/sprints/use-sprints";

/** A card with what the board shows beyond the application itself: match, open gaps, interview date. */
export type BoardItem = Application & { score: number | null; openGaps: number; interviewAt: string | null };

type GapRow = { postingId: string; done: boolean };

/** Cards of one sprint, joined with the postings (score, interview) and the gap plan (open gaps). `items` is null while loading. */
export function useBoardData(sprintId: string | undefined) {
    const [items, setItems] = useState<BoardItem[] | null>(null);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        if (!sprintId) {
            setItems([]);
            return;
        }
        setError(null);
        try {
            const [apps, postings, gaps] = await Promise.all([
                api.get<Application[]>(`/api/applications?sprintId=${sprintId}`),
                api.get<PostingSummary[]>("/api/postings"),
                api.get<GapRow[]>("/api/gaps"),
            ]);
            const byPosting = new Map((postings ?? []).map((p) => [p.id, p]));
            const open = new Map<string, number>();
            for (const g of gaps ?? []) if (!g.done) open.set(g.postingId, (open.get(g.postingId) ?? 0) + 1);
            setItems(
                (apps ?? []).map((a) => {
                    const p = byPosting.get(a.postingId);
                    return { ...a, score: p?.analysed && p.score != null ? p.score : null, openGaps: open.get(a.postingId) ?? 0, interviewAt: p?.interviewAt ?? null };
                }),
            );
        } catch (e) {
            setError(errorMessage(e));
        }
    }, [sprintId]);

    useEffect(() => {
        load();
    }, [load]);

    const patch = (id: string, change: Partial<BoardItem>) => setItems((cur) => (cur ?? []).map((a) => (a.id === id ? { ...a, ...change } : a)));

    return { items, error, load, patch };
}
