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

export type ProfileItemKind = "Skill" | "Project" | "Experience" | "Language" | "Education" | "Certification";

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
    quote: string;
    confirmed: boolean;
    evidence: Evidence[];
}

export interface Profile {
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
