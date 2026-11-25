import type { Stage } from "@/features/applications/types";

export type Sprint = {
    id: string;
    name: string;
    goal: string;
    startsOn: string;
    endsOn: string;
    status: "active" | "completed";
    completedAt: string | null;
    items: number;
    open: number;
    applied: number;
    interviews: number;
    offers: number;
    rejected: number;
};

export type SprintReportItem = { applicationId: string; postingId: string; key: string; company: string; title: string; stage: Stage; score: number | null };

export type SprintReport = { sprint: Sprint; items: SprintReportItem[]; averageScore: number | null; gapsClosed: number };

export type SprintDraft = { name: string; goal: string; startsOn: string; endsOn: string };

export type CompleteResult = { completed: Sprint; next: Sprint | null; carried: number; returnedToBacklog: number };

export type ApplicationEvent = {
    type: "added" | "stageChanged" | "sprintChanged";
    fromStage: Stage | null;
    toStage: Stage | null;
    sprintId: string | null;
    sprintName: string | null;
    at: string;
};

const DAY = 86_400_000;
const isoDay = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;

/** Draft for the next sprint: starts today, runs two weeks. */
export function newDraft(number: number): SprintDraft {
    const today = new Date();
    return { name: `Sprint ${number}`, goal: "", startsOn: isoDay(today), endsOn: isoDay(new Date(today.getTime() + 14 * DAY)) };
}

/** Whole days from today to the sprint end; negative once it is over. */
export function daysLeft(sprint: Sprint): number {
    const end = new Date(`${sprint.endsOn}T00:00:00`).getTime();
    const today = new Date(`${isoDay(new Date())}T00:00:00`).getTime();
    return Math.round((end - today) / DAY);
}

export function daysLeftLabel(sprint: Sprint): string {
    const n = daysLeft(sprint);
    if (n < 0) return `${-n} ${-n === 1 ? "day" : "days"} over`;
    if (n === 0) return "Ends today";
    return `${n} ${n === 1 ? "day" : "days"} left`;
}

/** Share of the sprint's positions that left Saved (applied or further). */
export function sprintProgress(sprint: Sprint): number {
    return sprint.items === 0 ? 0 : Math.round((sprint.applied / sprint.items) * 100);
}
