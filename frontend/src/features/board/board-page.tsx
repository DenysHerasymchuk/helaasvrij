import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router";
import { BarChart01 } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { api } from "@/core/api";
import { ErrorState, LoadingState, cardClass } from "@/core/ui";
import { type Stage, stageLabel, stages } from "@/features/applications/types";
import { AddPostingModal } from "@/features/postings/add-posting-modal";
import { CompleteSprintModal } from "@/features/sprints/complete-sprint-modal";
import { SprintBand } from "@/features/sprints/sprint-band";
import { StartSprintModal } from "@/features/sprints/start-sprint-modal";
import { errorMessage, useSprints } from "@/features/sprints/use-sprints";
import { BoardDrawer } from "./board-drawer";
import { BoardHelp } from "./board-help";
import { BoardInsights } from "./board-insights";
import { BoardToolbar } from "./board-toolbar";
import { BoardView } from "./board-view";
import { useBoardData } from "./use-board-data";
import { applyView, useViewOptions } from "./view-options";

type Toast = { text: string; undo?: () => void };

export function BoardPage() {
    const navigate = useNavigate();
    const { sprints, active, error: sprintError, reload } = useSprints();
    const { items, error, load, patch } = useBoardData(active?.id);
    const { options, update } = useViewOptions();
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [openId, setOpenId] = useState<string | null>(null);
    const [notice, setNotice] = useState<string | null>(null);
    const [toast, setToast] = useState<Toast | null>(null);
    const [starting, setStarting] = useState(false);
    const [completing, setCompleting] = useState(false);
    const [adding, setAdding] = useState(false);
    const [help, setHelp] = useState(false);
    const [insights, setInsights] = useState(false);
    const searchRef = useRef<HTMLInputElement>(null);
    const toastTimer = useRef<number>(0);

    const itemsRef = useRef(items);
    itemsRef.current = items;

    const shown = useMemo(() => applyView(items ?? [], options), [items, options]);
    const companies = useMemo(() => [...new Set((items ?? []).map((i) => i.company))].sort(), [items]);
    const opened = items?.find((i) => i.id === openId) ?? null;

    function say(next: Toast) {
        window.clearTimeout(toastTimer.current);
        setToast(next);
        toastTimer.current = window.setTimeout(() => setToast(null), 7000);
    }

    // Moves cards at once and puts them back if the server says no. A move can be undone from the toast.
    async function move(ids: string[], stage: Stage, quiet = false) {
        // Undo runs from an old closure, so read the cards as they are now.
        const moving = (itemsRef.current ?? []).filter((i) => ids.includes(i.id) && i.stage !== stage);
        if (moving.length === 0) return;
        setNotice(null);
        const now = new Date().toISOString();
        for (const m of moving) patch(m.id, { stage, stageChangedAt: now });
        const failed: string[] = [];
        for (const m of moving) {
            try {
                const saved = await api.patch<{ stage: Stage; stageChangedAt: string; appliedAt: string | null }>(`/api/applications/${m.id}`, { stage });
                if (saved) patch(m.id, { stage: saved.stage, stageChangedAt: saved.stageChangedAt, appliedAt: saved.appliedAt });
            } catch (e) {
                patch(m.id, { stage: m.stage, stageChangedAt: m.stageChangedAt });
                failed.push(m.title);
                setNotice(`Could not move "${m.title}" to ${stageLabel(stage)}. ${errorMessage(e)}`);
            }
        }
        reload();
        const done = moving.filter((m) => !failed.includes(m.title));
        if (!quiet && done.length > 0) {
            say({
                text: done.length === 1 ? `Moved "${done[0].title}" to ${stageLabel(stage)}.` : `Moved ${done.length} cards to ${stageLabel(stage)}.`,
                undo: async () => {
                    setToast(null);
                    for (const m of done) await move([m.id], m.stage, true);
                },
            });
        }
        setSelected(new Set());
    }

    async function saveNotes(id: string, notes: string) {
        const saved = await api.patch<{ notes: string }>(`/api/applications/${id}`, { notes });
        if (saved) patch(id, { notes: saved.notes });
    }

    async function remove(ids: string[]) {
        if (!active) return;
        setNotice(null);
        for (const i of (items ?? []).filter((x) => ids.includes(x.id))) {
            try {
                await api.del(`/api/sprints/${active.id}/items/${i.postingId}`);
            } catch (e) {
                setNotice(errorMessage(e));
            }
        }
        setOpenId(null);
        setSelected(new Set());
        await Promise.all([load(), reload()]);
    }

    async function addPosition(postingId: string) {
        if (!active) return;
        try {
            await api.post(`/api/sprints/${active.id}/items`, { postingIds: [postingId] });
            await Promise.all([load(), reload()]);
        } catch (e) {
            setNotice(errorMessage(e));
        }
    }

    const toggle = (id: string) =>
        setSelected((cur) => {
            const next = new Set(cur);
            if (!next.delete(id)) next.add(id);
            return next;
        });

    const collapse = (key: string) => update({ collapsed: options.collapsed.includes(key) ? options.collapsed.filter((k) => k !== key) : [...options.collapsed, key] });

    // Board-wide keys: "/" searches, "?" lists the shortcuts, Escape clears the selection.
    useEffect(() => {
        const onKey = (e: KeyboardEvent) => {
            const t = e.target as HTMLElement;
            if (t.closest("input, textarea, select, [role=dialog]") || e.metaKey || e.ctrlKey || e.altKey) return;
            if (e.key === "/") {
                e.preventDefault();
                searchRef.current?.focus();
            } else if (e.key === "?") setHelp(true);
            else if (e.key === "Escape") setSelected(new Set());
        };
        window.addEventListener("keydown", onKey);
        return () => window.removeEventListener("keydown", onKey);
    }, []);

    const sprintCount = sprints?.length ?? 0;

    return (
        <div className="flex min-h-0 flex-1 flex-col gap-3">
            {notice && (
                <p role="alert" className="rounded-lg bg-error-primary px-4 py-2.5 text-sm text-error-primary ring-1 ring-error_subtle">
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
                                <Button size="md" color="secondary" iconLeading={BarChart01} onPress={() => setInsights(true)}>
                                    Insights
                                </Button>
                                <Button size="md" color="secondary" href="/backlog">
                                    Add positions
                                </Button>
                                <Button size="md" onPress={() => setCompleting(true)}>
                                    Complete sprint
                                </Button>
                            </>
                        }
                    />
                    <BoardToolbar options={options} update={update} companies={companies} searchRef={searchRef} onHelp={() => setHelp(true)} />

                    {selected.size > 0 && (
                        <div role="region" aria-label="Selected cards" className="flex flex-wrap items-center gap-2 rounded-lg bg-primary px-3 py-2 shadow-xs ring-2 ring-brand">
                            <p className="mr-2 text-sm font-semibold text-primary">{selected.size} selected</p>
                            {stages.map((s) => (
                                <Button key={s.id} size="sm" color="secondary" onPress={() => move([...selected], s.id)}>
                                    {s.label}
                                </Button>
                            ))}
                            <Button size="sm" color="secondary-destructive" onPress={() => remove([...selected])}>
                                Remove from sprint
                            </Button>
                            <Button size="sm" color="tertiary" onPress={() => setSelected(new Set())}>
                                Clear
                            </Button>
                        </div>
                    )}

                    {items.length === 0 && <p className="text-md text-tertiary">This sprint is empty. Add positions from the backlog, or use the + on Saved.</p>}
                    {items.length > 0 && shown.length === 0 && <p className="text-md text-tertiary">No cards match these filters.</p>}

                    <BoardView
                        items={shown}
                        group={options.group}
                        collapsed={options.collapsed}
                        onCollapse={collapse}
                        selected={selected}
                        onSelect={toggle}
                        onOpen={setOpenId}
                        onMove={move}
                        onAdd={() => setAdding(true)}
                    />
                </>
            )}

            <BoardDrawer
                item={opened}
                onClose={() => setOpenId(null)}
                onMove={(stage) => opened && move([opened.id], stage)}
                onSaveNotes={(notes) => (opened ? saveNotes(opened.id, notes) : Promise.resolve())}
                onRemove={() => opened && remove([opened.id])}
            />
            <BoardHelp isOpen={help} onClose={() => setHelp(false)} />
            {active && <BoardInsights sprint={active} isOpen={insights} onClose={() => setInsights(false)} />}

            {toast && (
                <div role="status" className="fixed bottom-6 left-1/2 z-50 flex -translate-x-1/2 items-center gap-4 rounded-lg bg-primary px-4 py-3 text-sm text-primary shadow-lg ring-1 ring-secondary_alt">
                    <span>{toast.text}</span>
                    {toast.undo && (
                        <button type="button" onClick={toast.undo} className="cursor-pointer rounded-xs font-semibold text-brand-secondary outline-focus-ring hover:underline focus-visible:outline-2">
                            Undo
                        </button>
                    )}
                </div>
            )}

            <StartSprintModal isOpen={starting} count={sprintCount} onClose={() => setStarting(false)} onStarted={() => { setStarting(false); reload(); }} />
            {active && (
                <CompleteSprintModal sprint={active} count={sprintCount} isOpen={completing} onClose={() => setCompleting(false)} onDone={(result) => navigate(`/sprints/${result.completed.id}`)} />
            )}
            <AddPostingModal
                isOpen={adding}
                onClose={() => setAdding(false)}
                onAdded={(posting) => {
                    setAdding(false);
                    void addPosition(posting.id);
                }}
            />
        </div>
    );
}
