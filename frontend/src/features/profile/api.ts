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
