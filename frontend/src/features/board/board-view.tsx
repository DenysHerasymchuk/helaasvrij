import { type KeyboardEvent, useMemo, useRef, useState } from "react";
import { ChevronDown, ChevronRight, Plus } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { type Stage, stages } from "@/features/applications/types";
import { cx } from "@/utils/cx";
import { BoardCard, DRAG_TYPE } from "./board-card";
import type { BoardItem } from "./use-board-data";
import type { Group } from "./view-options";

const empty: Record<Stage, string> = {
    saved: "Positions you plan to apply to. Drag one here.",
    applied: "Sent and waiting. Drop a card when you apply.",
    interview: "They want to talk. Drop a card here.",
    offer: "A yes. Drop a card here.",
    rejected: "Not this time. Drop a card here.",
};

type Props = {
    items: BoardItem[];
    group: Group;
    collapsed: string[];
    onCollapse: (key: string) => void;
    selected: Set<string>;
    onSelect: (id: string) => void;
    onOpen: (id: string) => void;
    onMove: (ids: string[], stage: Stage) => void;
    onAdd: () => void;
};

/** The columns of the running sprint: cards, drag between columns, one lane per company when grouped. */
export function BoardView({ items, group, collapsed, onCollapse, selected, onSelect, onOpen, onMove, onAdd }: Props) {
    const root = useRef<HTMLDivElement>(null);
    const [over, setOver] = useState<string | null>(null);

    const lanes = useMemo(() => {
        if (group === "none") return [{ key: "all", title: "", items }];
        const map = new Map<string, BoardItem[]>();
        for (const i of items) map.set(i.company, [...(map.get(i.company) ?? []), i]);
        return [...map.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([title, list]) => ({ key: `lane:${title}`, title, items: list }));
    }, [items, group]);

    const colCollapsed = (s: Stage) => collapsed.includes(`col:${s}`);
    const template = stages.map((s) => (colCollapsed(s.id) ? "3rem" : "minmax(13rem,1fr)")).join(" ");
    const count = (s: Stage) => items.filter((i) => i.stage === s).length;

    const focusCard = (el: Element | null | undefined) => (el as HTMLElement | null | undefined)?.focus();

    function onKeyDown(e: KeyboardEvent<HTMLDivElement>) {
        const card = (e.target as HTMLElement).closest<HTMLElement>("[data-card-id]");
        if (!card || (e.target as HTMLElement) !== card) return;
        const id = card.dataset.cardId!;
        const cell = card.closest<HTMLElement>("[data-cell]")!;
        const cards = [...cell.querySelectorAll<HTMLElement>("[data-card-id]")];
        const at = cards.indexOf(card);
        const side = (by: number) => {
            const i = stages.findIndex((s) => s.id === cell.dataset.col) + by;
            const next = stages[i];
            return next && root.current?.querySelector(`[data-cell][data-lane="${CSS.escape(cell.dataset.lane!)}"][data-col="${next.id}"] [data-card-id]`);
        };
        const digit = Number(e.key);
        if (e.key === "ArrowDown") focusCard(cards[at + 1]);
        else if (e.key === "ArrowUp") focusCard(cards[at - 1]);
        else if (e.key === "ArrowRight") focusCard(side(1));
        else if (e.key === "ArrowLeft") focusCard(side(-1));
        else if (e.key === "Enter") onOpen(id);
        else if (e.key === "x") onSelect(id);
        else if (e.key === "m") card.querySelector<HTMLElement>("[data-move]")?.click();
        else if (digit >= 1 && digit <= stages.length) onMove(selected.has(id) ? [...selected] : [id], stages[digit - 1].id);
        else return;
        e.preventDefault();
    }

    return (
        <div ref={root} onKeyDown={onKeyDown} className="min-h-0 flex-1 overflow-auto rounded-xl bg-secondary p-2 ring-1 ring-secondary">
            <div className="grid min-w-max gap-2 lg:min-w-0" style={{ gridTemplateColumns: template }}>
                {stages.map((s) =>
                    colCollapsed(s.id) ? (
                        <div key={s.id} className="sticky top-0 z-10 flex flex-col items-center gap-2 rounded-lg bg-primary py-3 ring-1 ring-secondary">
                            <Button color="tertiary" size="sm" iconLeading={ChevronRight} aria-label={`Expand ${s.label}`} onPress={() => onCollapse(`col:${s.id}`)} />
                            <Badge type="pill-color" size="sm" color={s.color}>
                                {count(s.id)}
                            </Badge>
                            <span className="text-sm font-semibold text-primary [writing-mode:vertical-rl]">{s.label}</span>
                        </div>
                    ) : (
                        <header key={s.id} className="sticky top-0 z-10 flex items-center justify-between gap-2 rounded-lg bg-primary px-3 py-2 ring-1 ring-secondary">
                            <div className="min-w-0">
                                <h2 className="text-sm font-semibold text-primary">{s.label}</h2>
                                <p className="truncate text-xs text-tertiary">{s.hint}</p>
                            </div>
                            <div className="flex shrink-0 items-center gap-1">
                                <Badge type="pill-color" size="sm" color={s.color}>
                                    {count(s.id)}
                                </Badge>
                                {s.id === "saved" && <Button color="tertiary" size="sm" iconLeading={Plus} aria-label="Add a position to this sprint" onPress={onAdd} />}
                                <Button color="tertiary" size="sm" iconLeading={ChevronDown} aria-label={`Collapse ${s.label}`} onPress={() => onCollapse(`col:${s.id}`)} />
                            </div>
                        </header>
                    ),
                )}
            </div>

            {lanes.map((lane) => {
                const open = !collapsed.includes(lane.key);
                return (
                    <section key={lane.key} aria-label={lane.title || "Positions"} className="mt-2">
                        {lane.title && (
                            <button
                                type="button"
                                onClick={() => onCollapse(lane.key)}
                                aria-expanded={open}
                                className="mb-2 flex w-full cursor-pointer items-center gap-2 rounded-lg px-2 py-1 text-left text-sm font-semibold text-secondary outline-focus-ring hover:bg-primary_hover focus-visible:outline-2"
                            >
                                {open ? <ChevronDown className="size-4" aria-hidden="true" /> : <ChevronRight className="size-4" aria-hidden="true" />}
                                {lane.title}
                                <span className="font-normal text-tertiary">({lane.items.length})</span>
                            </button>
                        )}
                        {open && (
                            <div className="grid min-w-max gap-2 lg:min-w-0" style={{ gridTemplateColumns: template }}>
                                {stages.map((s) => {
                                    const cellKey = `${lane.key}|${s.id}`;
                                    const cards = lane.items.filter((i) => i.stage === s.id);
                                    return (
                                        <div
                                            key={s.id}
                                            data-cell
                                            data-lane={lane.key}
                                            data-col={s.id}
                                            onDragOver={(e) => {
                                                if (e.dataTransfer.types.includes(DRAG_TYPE)) {
                                                    e.preventDefault();
                                                    setOver(cellKey);
                                                }
                                            }}
                                            onDragLeave={(e) => {
                                                if (!e.currentTarget.contains(e.relatedTarget as Node)) setOver(null);
                                            }}
                                            onDrop={(e) => {
                                                e.preventDefault();
                                                setOver(null);
                                                const raw = e.dataTransfer.getData(DRAG_TYPE);
                                                if (raw) onMove(JSON.parse(raw) as string[], s.id);
                                            }}
                                            className={cx(
                                                "flex flex-col gap-2 rounded-lg p-1.5 transition",
                                                lane.key === "all" ? "min-h-24" : "min-h-12",
                                                colCollapsed(s.id) && "items-center",
                                                over === cellKey ? "bg-brand-primary ring-2 ring-brand" : "bg-primary/40",
                                            )}
                                        >
                                            {colCollapsed(s.id) ? (
                                                <span className="text-xs text-tertiary">{cards.length}</span>
                                            ) : cards.length === 0 ? (
                                                // Company lanes repeat every column, so only the plain board spells out what a column is for.
                                                lane.key === "all" && <p className="rounded-lg border border-dashed border-secondary px-3 py-6 text-center text-sm text-quaternary">{empty[s.id]}</p>
                                            ) : (
                                                cards.map((i) => (
                                                    <BoardCard
                                                        key={i.id}
                                                        item={i}
                                                        selected={selected.has(i.id)}
                                                        onSelect={() => onSelect(i.id)}
                                                        onOpen={() => onOpen(i.id)}
                                                        onMove={(stage) => onMove(selected.has(i.id) ? [...selected] : [i.id], stage)}
                                                        dragIds={selected.has(i.id) ? [...selected] : [i.id]}
                                                    />
                                                ))
                                            )}
                                        </div>
                                    );
                                })}
                            </div>
                        )}
                    </section>
                );
            })}
        </div>
    );
}
