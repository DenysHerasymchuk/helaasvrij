import type { DashboardPosting, DashboardRequirement } from "./use-dashboard";

export const dateFmt = new Intl.DateTimeFormat("en-BE", { day: "numeric", month: "short" });
export const timeFmt = new Intl.DateTimeFormat("en-BE", { hour: "2-digit", minute: "2-digit" });
export const fmt = (iso: string) => dateFmt.format(new Date(iso));

export const ranked = (postings: DashboardPosting[]) => [...postings].sort((a, b) => b.score - a.score);

export type NextStep = DashboardRequirement & { company: string };

/** Open gaps with an action, biggest gain first, then earliest due date. */
export const nextSteps = (postings: DashboardPosting[]): NextStep[] =>
    postings
        .flatMap((p) => p.requirements.filter((r) => r.action && !r.done && r.coverage !== "strong").map((r) => ({ ...r, company: p.company })))
        .sort((a, b) => b.gain - a.gain || (a.due ?? "9999").localeCompare(b.due ?? "9999"));

const today = () => new Date().toISOString().slice(0, 10);

/** Closest deadline that has not passed yet. */
export const nextDeadline = (postings: DashboardPosting[]) =>
    postings
        .filter((p): p is DashboardPosting & { deadline: string } => p.deadline !== null && p.deadline >= today())
        .sort((a, b) => a.deadline.localeCompare(b.deadline))[0];

/** Closest interview that has not happened yet. */
export const nextInterview = (postings: DashboardPosting[]) =>
    postings
        .filter((p): p is DashboardPosting & { interviewAt: string } => p.interviewAt !== null && new Date(p.interviewAt).getTime() >= Date.now())
        .sort((a, b) => a.interviewAt.localeCompare(b.interviewAt))[0];
