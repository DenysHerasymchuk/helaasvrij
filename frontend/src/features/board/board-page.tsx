import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { Button } from "@/components/base/buttons/button";
import { api } from "@/core/api";
import { ErrorState, LoadingState, cardClass } from "@/core/ui";
import { Board } from "@/features/applications/board";
import { type Application, type Stage, stageLabel } from "@/features/applications/types";
import { CompleteSprintModal } from "@/features/sprints/complete-sprint-modal";
import { SprintBand } from "@/features/sprints/sprint-band";
import { StartSprintModal } from "@/features/sprints/start-sprint-modal";
import { errorMessage, useSprints } from "@/features/sprints/use-sprints";

export function BoardPage() {
    const navigate = useNavigate();
    const { sprints, active, error: sprintError, reload } = useSprints();
    const [items, setItems] = useState<Application[] | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [notice, setNotice] = useState<string | null>(null);
    const [starting, setStarting] = useState(false);
    const [completing, setCompleting] = useState(false);
    const activeId = active?.id;

    const load = useCallback(async () => {
        if (!activeId) {
            setItems([]);
            return;
        }
        setError(null);
        try {
            setItems((await api.get<Application[]>(`/api/applications?sprintId=${activeId}`)) ?? []);
        } catch (e) {
            setError(errorMessage(e));
        }
    }, [activeId]);

    useEffect(() => {
        if (sprints) load();
    }, [sprints, load]);

    const replace = (updated: Application) => setItems((current) => (current ?? []).map((a) => (a.id === updated.id ? updated : a)));

    async function move(id: string, stage: Stage) {
        const before = items?.find((a) => a.id === id);
        if (!before || before.stage === stage) return;
        setNotice(null);
        // Optimistic: the card jumps at once, and goes back if the server says no.
        replace({ ...before, stage, stageChangedAt: new Date().toISOString() });
        try {
            const saved = await api.patch<Application>(`/api/applications/${id}`, { stage });
            if (saved) replace(saved);
            reload();
        } catch (e) {
            replace(before);
            setNotice(`Could not move "${before.title}" to ${stageLabel(stage)}. ${errorMessage(e)}`);
        }
    }

    async function saveNotes(id: string, notes: string) {
        const saved = await api.patch<Application>(`/api/applications/${id}`, { notes });
        if (saved) replace(saved);
    }

    return (
        <div className="flex w-full flex-col gap-6">
            {notice && (
                <p role="alert" className="rounded-lg bg-error-primary px-4 py-3 text-sm text-error-primary ring-1 ring-error_subtle">
                    {notice}
                </p>
            )}

            {sprintError || error ? (
                <ErrorState message={(sprintError ?? error)!} onRetry={() => { reload(); load(); }} />
            ) : sprints === null || items === null ? (
                <LoadingState>Loading the board...</LoadingState>
            ) : !active ? (
                <section className={`${cardClass} flex flex-col items-start gap-3 p-6`}>
                    <p className="text-md font-semibold text-primary">No sprint is running.</p>
                    <p className="text-md text-tertiary">Start a sprint, then pull positions in from your backlog. They show up here as cards.</p>
                    <div className="flex flex-wrap gap-2">
                        <Button size="md" onPress={() => setStarting(true)}>
                            Start a sprint
                        </Button>
                        <Button size="md" color="secondary" href="/backlog">
                            Open backlog
                        </Button>
                    </div>
                </section>
            ) : (
                <>
                    <SprintBand
                        sprint={active}
                        action={
                            <>
                                <Button size="md" color="secondary" href="/backlog">
                                    Add positions
                                </Button>
                                <Button size="md" onPress={() => setCompleting(true)}>
                                    Complete sprint
                                </Button>
                            </>
                        }
                    />
                    {items.length === 0 && (
                        <p className="text-md text-tertiary">This sprint is empty. Add positions from the backlog to see cards here.</p>
                    )}
                    <Board items={items} onMove={move} onSaveNotes={saveNotes} />
                </>
            )}

            <StartSprintModal
                isOpen={starting}
                count={sprints?.length ?? 0}
                onClose={() => setStarting(false)}
                onStarted={() => {
                    setStarting(false);
                    reload();
                }}
            />
            {active && (
                <CompleteSprintModal
                    sprint={active}
                    count={sprints?.length ?? 0}
                    isOpen={completing}
                    onClose={() => setCompleting(false)}
                    onDone={(result) => navigate(`/sprints/${result.completed.id}`)}
                />
            )}
        </div>
    );
}
