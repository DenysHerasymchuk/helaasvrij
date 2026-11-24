import { useCallback, useEffect, useMemo, useState } from "react";
import { Plus } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { JourneyBar } from "@/features/journey/journey-bar";
import { Field, NativeSelect } from "@/features/postings/fields";
import type { PostingSummary } from "@/features/postings/types";
import { Board } from "./board";
import { type Application, type Stage, stageLabel } from "./types";

const message = (e: unknown) => (e instanceof ApiError ? e.message : "Something went wrong. Try again.");

export function ApplicationsPage() {
    const [items, setItems] = useState<Application[] | null>(null);
    const [postings, setPostings] = useState<PostingSummary[]>([]);
    const [error, setError] = useState<string | null>(null);
    const [notice, setNotice] = useState<string | null>(null);
    const [pick, setPick] = useState("");
    const [adding, setAdding] = useState(false);

    const load = useCallback(async () => {
        setError(null);
        setItems(null);
        try {
            const [apps, list] = await Promise.all([api.get<Application[]>("/api/applications"), api.get<PostingSummary[]>("/api/postings")]);
            setItems(apps ?? []);
            setPostings(list ?? []);
        } catch (e) {
            setError(message(e));
        }
    }, []);

    useEffect(() => {
        load();
    }, [load]);

    const untracked = useMemo(() => {
        const tracked = new Set((items ?? []).map((a) => a.postingId));
        return postings.filter((p) => !tracked.has(p.id));
    }, [items, postings]);

    const replace = (updated: Application) => setItems((current) => (current ?? []).map((a) => (a.id === updated.id ? updated : a)));

    async function add() {
        if (!pick) return;
        setAdding(true);
        setNotice(null);
        try {
            const created = await api.post<Application>("/api/applications", { postingId: pick });
            if (created) setItems((current) => [created, ...(current ?? [])]);
            setPick("");
        } catch (e) {
            setNotice(message(e));
        } finally {
            setAdding(false);
        }
    }

    async function move(id: string, stage: Stage) {
        const before = items?.find((a) => a.id === id);
        if (!before || before.stage === stage) return;
        setNotice(null);
        // Optimistic: the card jumps at once, and goes back if the server says no.
        replace({ ...before, stage, stageChangedAt: new Date().toISOString() });
        try {
            const saved = await api.patch<Application>(`/api/applications/${id}`, { stage });
            if (saved) replace(saved);
        } catch (e) {
            replace(before);
            setNotice(`Could not move "${before.title}" to ${stageLabel(stage)}. ${message(e)}`);
        }
    }

    async function saveNotes(id: string, notes: string) {
        const saved = await api.patch<Application>(`/api/applications/${id}`, { notes });
        if (saved) replace(saved);
    }

    return (
        <div className="mx-auto flex max-w-[1400px] flex-col gap-6 px-4 py-8 md:px-8">
            <div className="flex flex-wrap items-end justify-between gap-4">
                <div>
                    <h1 className="text-display-xs font-semibold text-primary">Applications</h1>
                    <p className="mt-1 text-md text-tertiary">Drag a card to a new column, or use Move to.</p>
                </div>
                {items && postings.length > 0 && (
                    <div className="flex w-full items-end gap-2 sm:w-auto">
                        <div className="min-w-0 flex-1 sm:w-72 sm:flex-none">
                            <Field label="Track a posting">
                                <NativeSelect value={pick} onChange={(e) => setPick(e.target.value)} disabled={untracked.length === 0}>
                                    <option value="">{untracked.length === 0 ? "All postings are tracked" : "Choose a posting"}</option>
                                    {untracked.map((p) => (
                                        <option key={p.id} value={p.id}>
                                            {p.title} at {p.company}
                                        </option>
                                    ))}
                                </NativeSelect>
                            </Field>
                        </div>
                        <Button size="md" iconLeading={Plus} isDisabled={!pick} isLoading={adding} onPress={add}>
                            Add
                        </Button>
                    </div>
                )}
            </div>
            <JourneyBar />

            {notice && (
                <p role="alert" className="rounded-lg bg-error-primary px-4 py-3 text-sm text-error-primary ring-1 ring-error_subtle">
                    {notice}
                </p>
            )}

            {error ? (
                <section className="flex flex-col items-start gap-3 rounded-xl bg-primary p-6 shadow-xs ring-1 ring-secondary">
                    <p role="alert" className="text-md text-error-primary">
                        {error}
                    </p>
                    <Button color="secondary" size="md" onPress={load}>
                        Try again
                    </Button>
                </section>
            ) : items === null ? (
                <p role="status" className="rounded-xl bg-primary p-6 text-md text-tertiary shadow-xs ring-1 ring-secondary">
                    Loading applications...
                </p>
            ) : (
                <>
                    {items.length === 0 && (
                        <section className="flex flex-col items-start gap-3 rounded-xl bg-primary p-6 shadow-xs ring-1 ring-secondary">
                            <p className="text-md font-semibold text-primary">No applications yet.</p>
                            <p className="text-md text-tertiary">
                                {postings.length === 0
                                    ? "Add a posting first, then track it here from Saved to Offer."
                                    : "Pick a posting above to start tracking it."}
                            </p>
                            {postings.length === 0 && (
                                <Button size="md" href="/postings">
                                    Go to postings
                                </Button>
                            )}
                        </section>
                    )}
                    <Board items={items} onMove={move} onSaveNotes={saveNotes} />
                </>
            )}
        </div>
    );
}
