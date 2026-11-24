export type PostingLanguage = "nl" | "fr" | "en";

export type PostingSummary = {
    id: string;
    company: string;
    title: string;
    city: string;
    language: PostingLanguage;
    deadline: string | null;
    interviewAt: string | null;
    isDemo: boolean;
    requirementCount: number;
    createdAt: string;
    /** Internship dates from the posting (yyyy-MM-dd). */
    stageStart: string | null;
    stageEnd: string | null;
    /** Posting dates against the stage period on the profile. */
    stageFit: StageFit;
};

export type StageFit = "fits" | "conflict" | "unknown";

export type Coverage = "none" | "partial" | "strong";

export type EvidenceSource = { sourceId: string; kind: string; label: string; path: string; url: string | null };

export type PostingRequirement = {
    id: string;
    text: string;
    importance: "required" | "nice";
    coverage: Coverage;
    evidenceQuote: string | null;
    evidenceSource: EvidenceSource | null;
    action: string | null;
    actionProject: string | null;
    dueDate: string | null;
    done: boolean;
    analysedAt: string | null;
};

export type GapAnalysis = { analysed: boolean; score: number; noClearFit: boolean; analysedAt: string | null };

export type PostingDetail = Omit<PostingSummary, "requirementCount"> & {
    url: string | null;
    rawText: string;
    requirements: PostingRequirement[];
    analysis: GapAnalysis;
};

const dateFmt = new Intl.DateTimeFormat("en-BE", { day: "numeric", month: "short", year: "numeric" });
const dateTimeFmt = new Intl.DateTimeFormat("en-BE", { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });

export const formatDate = (iso: string) => dateFmt.format(new Date(`${iso.slice(0, 10)}T00:00:00`));
export const formatDateTime = (iso: string) => dateTimeFmt.format(new Date(iso));

/** ISO instant to the value of an `<input type="datetime-local">` (local time, no zone). */
export function toLocalInput(iso: string | null): string {
    if (!iso) return "";
    const d = new Date(iso);
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** "1 Feb 2027 to 31 May 2027", "from 1 Feb 2027", or null when the posting has no stage dates. */
export function formatStagePeriod(start: string | null, end: string | null): string | null {
    if (start && end) return `${formatDate(start)} to ${formatDate(end)}`;
    if (start) return `from ${formatDate(start)}`;
    if (end) return `until ${formatDate(end)}`;
    return null;
}
