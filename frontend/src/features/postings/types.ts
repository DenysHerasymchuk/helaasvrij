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
};

export type PostingRequirement = { id: string; text: string; importance: "required" | "nice" };

export type PostingDetail = Omit<PostingSummary, "requirementCount"> & {
    url: string | null;
    rawText: string;
    requirements: PostingRequirement[];
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
