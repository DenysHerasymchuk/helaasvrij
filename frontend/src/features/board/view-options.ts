import { useState } from "react";
import type { BoardItem } from "./use-board-data";

export type Sort = "recent" | "match" | "deadline";
export type Group = "none" | "company";

export type ViewOptions = {
    query: string;
    company: string;
    soon: boolean;
    notes: boolean;
    strong: boolean;
    sort: Sort;
    group: Group;
    collapsed: string[];
};

const KEY = "board-view";
export const defaults: ViewOptions = { query: "", company: "", soon: false, notes: false, strong: false, sort: "recent", group: "none", collapsed: [] };

/** View choices (filters, sort, grouping, collapsed columns), kept in this browser. The search text is not kept. */
export function useViewOptions() {
    const [options, setOptions] = useState<ViewOptions>(() => {
        try {
            return { ...defaults, ...JSON.parse(localStorage.getItem(KEY) ?? "{}"), query: "" };
        } catch {
            return defaults;
        }
    });

    const update = (change: Partial<ViewOptions>) =>
        setOptions((current) => {
            const next = { ...current, ...change };
            try {
                localStorage.setItem(KEY, JSON.stringify({ ...next, query: "" }));
            } catch {
                // Storage blocked: the choice lasts for this page only.
            }
            return next;
        });

    return { options, update };
}

const DAY = 86_400_000;
const soonLimit = 7 * DAY;

/** Days from now to a deadline date (yyyy-MM-dd); negative once it is over. */
export const daysUntil = (date: string) => Math.ceil((new Date(`${date}T23:59:59`).getTime() - Date.now()) / DAY);

export const daysInStage = (item: BoardItem) => Math.max(0, Math.floor((Date.now() - new Date(item.stageChangedAt).getTime()) / DAY));

export function applyView(items: BoardItem[], o: ViewOptions): BoardItem[] {
    const q = o.query.trim().toLowerCase();
    const now = Date.now();
    const out = items.filter(
        (i) =>
            (!q || `${i.key} ${i.title} ${i.company} ${i.city}`.toLowerCase().includes(q)) &&
            (!o.company || i.company === o.company) &&
            (!o.soon || (i.deadline != null && new Date(`${i.deadline}T23:59:59`).getTime() - now <= soonLimit && i.stage === "saved")) &&
            (!o.notes || i.notes.trim() !== "") &&
            (!o.strong || (i.score ?? 0) >= 70),
    );
    const by: Record<Sort, (a: BoardItem, b: BoardItem) => number> = {
        recent: (a, b) => b.stageChangedAt.localeCompare(a.stageChangedAt),
        match: (a, b) => (b.score ?? -1) - (a.score ?? -1),
        deadline: (a, b) => (a.deadline ?? "9999").localeCompare(b.deadline ?? "9999"),
    };
    return out.sort(by[o.sort]);
}
