import { formatDate, formatDateTime } from "@/features/postings/types";
import { nextSteps } from "./dashboard-data";
import type { Home } from "./use-home";

export type FocusItem = {
    id: string;
    tone: "interview" | "deadline" | "gap" | "check";
    title: string;
    detail: string;
    to: string;
    cta: string;
};

const DAY = 86_400_000;
const WEEK = 7 * DAY;

const inDays = (date: string) => Math.ceil((new Date(`${date.slice(0, 10)}T23:59:59`).getTime() - Date.now()) / DAY);
const when = (days: number) => (days <= 0 ? "today" : days === 1 ? "tomorrow" : `in ${days} days`);

/** What to do today, most urgent first: interviews, deadlines, the gaps that raise a match most, positions never checked. */
export function buildFocus(home: Home): { items: FocusItem[]; sentence: string } {
    const now = Date.now();
    const started = new Set(home.applications.filter((a) => a.stage !== "saved").map((a) => a.postingId));
    const items: FocusItem[] = [];

    const interviews = home.postings
        .filter((p) => p.interviewAt && new Date(p.interviewAt).getTime() >= now && new Date(p.interviewAt).getTime() - now <= WEEK)
        .sort((a, b) => a.interviewAt!.localeCompare(b.interviewAt!));
    for (const p of interviews) {
        items.push({ id: `i-${p.id}`, tone: "interview", title: `Interview at ${p.company}`, detail: `${formatDateTime(p.interviewAt!)}, ${p.title}`, to: `/interview?postingId=${p.id}`, cta: "Prepare" });
    }

    const deadlines = home.postings
        .filter((p) => p.deadline && !started.has(p.id) && inDays(p.deadline) >= 0 && inDays(p.deadline) <= 7)
        .sort((a, b) => a.deadline!.localeCompare(b.deadline!));
    for (const p of deadlines) {
        items.push({ id: `d-${p.id}`, tone: "deadline", title: `Apply by ${formatDate(p.deadline!)} (${when(inDays(p.deadline!))})`, detail: `${p.title} at ${p.company}`, to: `/positions/${p.id}`, cta: "Open" });
    }

    for (const s of nextSteps(home.dashboard.postings).slice(0, 2)) {
        items.push({ id: `g-${s.id}`, tone: "gap", title: s.action!, detail: `Raises your match at ${s.company} by up to ${s.gain}%`, to: "/gaps", cta: "Gap plan" });
    }

    const unchecked = home.postings.filter((p) => !p.analysed).length;
    if (unchecked > 0) {
        items.push({ id: "check", tone: "check", title: `${unchecked} ${unchecked === 1 ? "position is" : "positions are"} not checked yet`, detail: "Check them against your profile to see your match and gaps.", to: "/gaps", cta: "Check" });
    }

    const parts: string[] = [];
    if (interviews.length > 0) parts.push(`${interviews.length === 1 ? "an interview" : `${interviews.length} interviews`} this week`);
    if (deadlines.length > 0) parts.push(`${deadlines.length === 1 ? "a deadline" : `${deadlines.length} deadlines`} within 7 days`);
    const sentence = parts.length > 0 ? `You have ${parts.join(" and ")}.` : items.length > 0 ? "Nothing is urgent. Here is what moves you forward." : "You are all caught up.";

    return { items: items.slice(0, 6), sentence };
}
