import { ApiError, api } from "@/core/api";

export type SourceKind = "GitHub" | "Cv" | "Site" | "PastedText";
export type SourceStatus = "Pending" | "Ready" | "Failed";

export interface Source {
    id: string;
    kind: SourceKind;
    label: string;
    status: SourceStatus;
    lastSyncedAt: string | null;
    error: string | null;
    documentCount: number;
    /** GitHub only: how many repositories were read. */
    repoCount?: number;
}

export const listSources = () => api.get<Source[]>("/api/sources");

export const syncGitHub = (username?: string) => api.post<Source>("/api/sources/github", { username });

export const addText = (label: string, text: string) => api.post<Source>("/api/sources/text", { label, text });

export const addSite = (url: string) => api.post<Source>("/api/sources/site", { url });

export const deleteSource = (id: string) => api.del(`/api/sources/${id}`);

/** The CV upload is multipart, so it bypasses the JSON-only `api` helper. */
export async function uploadCv(file: File): Promise<Source> {
    const form = new FormData();
    form.append("file", file);
    const response = await fetch("/api/sources/cv", { method: "POST", credentials: "same-origin", body: form });
    if (response.ok) return (await response.json()) as Source;
    let title = "Something went wrong. Try again.";
    try {
        const problem = await response.json();
        if (typeof problem.title === "string") title = problem.title;
    } catch {
        // Not JSON: keep the generic message.
    }
    throw new ApiError(response.status, title);
}

// ---- Profile items, extraction and basics ----

export type ProfileItemKind = "Skill" | "Project" | "Experience" | "Language" | "Education";

export interface Evidence {
    quote: string;
    sourceId: string;
    sourceKind: SourceKind;
    sourceLabel: string;
    documentPath: string;
    /** Public page of the document (a GitHub repo), when there is one. */
    url: string | null;
}

export interface ProfileItem {
    id: string;
    kind: ProfileItemKind;
    name: string;
    /** CEFR level, languages only. */
    level: string | null;
    quote: string;
    confirmed: boolean;
    evidence: Evidence[];
}

export interface ProfileBasics {
    school: string | null;
    /** yyyy-MM-dd */
    stageStart: string | null;
    stageEnd: string | null;
}

export interface Profile {
    basics: ProfileBasics;
    items: ProfileItem[];
}

export interface ExtractResult {
    created: number;
    merged: number;
    /** Claims dropped because their quote was not found in the source. */
    dropped: number;
    documents: number;
    skippedDocuments: number;
}

export const getProfile = () => api.get<Profile>("/api/profile");

export const extractProfile = () => api.post<ExtractResult>("/api/profile/extract");

export const deleteProfileItem = (id: string) => api.del(`/api/profile/items/${id}`);

export const confirmAllItems = () => api.post<{ confirmed: number }>("/api/profile/items/confirm-all");

export const setItemLevel = (id: string, level: string | null) => api.put(`/api/profile/items/${id}/level`, { level });

export const cefrLevels = ["A1", "A2", "B1", "B2", "C1", "C2"] as const;

export const saveBasics = (basics: ProfileBasics) => api.put<ProfileBasics>("/api/profile/basics", basics);

/** `api` has no PATCH helper; this keeps the same JSON-only contract and error type. */
export async function setItemConfirmed(id: string, confirmed: boolean): Promise<void> {
    const response = await fetch(`/api/profile/items/${id}`, {
        method: "PATCH",
        credentials: "same-origin",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ confirmed }),
    });
    if (response.ok) return;
    let title = "Something went wrong. Try again.";
    try {
        const problem = await response.json();
        if (typeof problem.title === "string") title = problem.title;
    } catch {
        // Not JSON: keep the generic message.
    }
    throw new ApiError(response.status, title);
}
