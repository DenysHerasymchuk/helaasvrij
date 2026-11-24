import { type Posting, type Requirement, impact, postings, score } from "@/features/demo/demo-user";

export const dateFmt = new Intl.DateTimeFormat("en-BE", { day: "numeric", month: "short" });
export const fmt = (iso: string) => dateFmt.format(new Date(iso));

export const ranked = [...postings].sort((a, b) => score(b.requirements) - score(a.requirements));

// Match points a posting gains when this requirement becomes fully covered.
const gain = (p: Posting, r: Requirement) => score(p.requirements.map((x) => (x === r ? { ...x, coverage: "strong" as const } : x))) - score(p.requirements);

export const actions = postings
    .flatMap((p) => p.requirements.filter((r) => r.action && impact(r) > 0).map((r) => ({ ...r, company: p.company, gain: gain(p, r) })))
    .sort((a, b) => b.gain - a.gain || (a.due ?? "").localeCompare(b.due ?? ""));

export const nextDeadline = [...postings].sort((a, b) => a.deadline.localeCompare(b.deadline))[0];
export const nextInterview = postings.find((p) => p.interviewAt);
