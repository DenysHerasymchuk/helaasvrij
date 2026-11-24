import { useState } from "react";
import {
    GridList,
    GridListItem,
    Menu,
    MenuItem,
    MenuTrigger,
    Popover,
    type DragAndDropHooks,
    useDragAndDrop,
} from "react-aria-components";
import { Link } from "react-router";
import { ArrowsRight, DotsGrid, Edit03 } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { ApiError } from "@/core/api";
import { InterviewDate } from "@/features/calendar/interview-date";
import { TextArea } from "@/features/postings/fields";
import { formatDate } from "@/features/postings/types";
import { type Application, type Stage, stages } from "./types";

const DRAG_TYPE = "application/x-application-id";

type BoardProps = {
    items: Application[];
    onMove: (id: string, stage: Stage) => void;
    onSaveNotes: (id: string, notes: string) => Promise<void>;
};

export function Board({ items, onMove, onSaveNotes }: BoardProps) {
    return (
        // Columns scroll sideways inside the board on a phone; the page itself never does.
        <div className="-mx-4 overflow-x-auto px-4 pb-2 md:mx-0 md:px-0">
            <div className="grid min-w-max grid-cols-5 gap-4 md:min-w-0 [&>*]:w-[272px] md:[&>*]:w-auto">
                {stages.map((stage) => (
                    <Column
                        key={stage.id}
                        stage={stage}
                        cards={items.filter((a) => a.stage === stage.id).sort((a, b) => b.stageChangedAt.localeCompare(a.stageChangedAt))}
                        onMove={onMove}
                        onSaveNotes={onSaveNotes}
                    />
                ))}
            </div>
        </div>
    );
}

function Column({ stage, cards, onMove, onSaveNotes }: { stage: (typeof stages)[number]; cards: Application[]; onMove: BoardProps["onMove"]; onSaveNotes: BoardProps["onSaveNotes"] }) {
    const { dragAndDropHooks } = useDragAndDrop({
        getItems: (keys) => [...keys].map((key) => ({ [DRAG_TYPE]: String(key), "text/plain": String(key) })),
        acceptedDragTypes: [DRAG_TYPE],
        getDropOperation: () => "move",
        onRootDrop: async (e) => {
            for (const item of e.items) {
                if (item.kind === "text") onMove(await item.getText(DRAG_TYPE), stage.id);
            }
        },
        onInsert: async (e) => {
            for (const item of e.items) {
                if (item.kind === "text") onMove(await item.getText(DRAG_TYPE), stage.id);
            }
        },
    });

    return (
        <section aria-labelledby={`col-${stage.id}`} className="flex min-h-48 flex-col rounded-xl bg-secondary ring-1 ring-secondary">
            <header className="flex items-center justify-between gap-2 px-4 pt-4 pb-3">
                <div className="min-w-0">
                    <h2 id={`col-${stage.id}`} className="text-sm font-semibold text-primary">
                        {stage.label}
                    </h2>
                    <p className="truncate text-xs text-tertiary">{stage.hint}</p>
                </div>
                <Badge type="pill-color" size="sm" color={stage.color}>
                    {cards.length}
                </Badge>
            </header>
            <GridList
                aria-label={`${stage.label} applications`}
                items={cards}
                dragAndDropHooks={dragAndDropHooks as DragAndDropHooks}
                selectionMode="none"
                className="flex flex-1 flex-col gap-2 px-3 pb-3 outline-hidden data-[drop-target]:rounded-lg data-[drop-target]:bg-brand-primary"
                renderEmptyState={() => <p className="rounded-lg border border-dashed border-secondary px-3 py-6 text-center text-sm text-quaternary">Drop a card here</p>}
            >
                {(card) => <ApplicationCard card={card} onMove={onMove} onSaveNotes={onSaveNotes} />}
            </GridList>
        </section>
    );
}

function ApplicationCard({ card, onMove, onSaveNotes }: { card: Application; onMove: BoardProps["onMove"]; onSaveNotes: BoardProps["onSaveNotes"] }) {
    const [editing, setEditing] = useState(false);

    return (
        <GridListItem
            id={card.id}
            textValue={`${card.title} at ${card.company}`}
            className="group flex flex-col gap-2 rounded-lg bg-primary p-3 shadow-xs ring-1 ring-secondary outline-focus-ring data-[dragging]:opacity-50 data-[focus-visible]:outline-2 data-[drop-target]:ring-2 data-[drop-target]:ring-brand"
        >
            <div className="flex items-start gap-2">
                <div className="min-w-0 flex-1">
                    <Link to={`/postings/${card.postingId}`} className="block truncate text-sm font-semibold text-primary outline-focus-ring hover:underline focus-visible:outline-2">
                        {card.title}
                    </Link>
                    <p className="truncate text-sm text-tertiary">
                        {card.company}
                        {card.city && `, ${card.city}`}
                    </p>
                </div>
                <Button slot="drag" color="tertiary" size="sm" iconLeading={DotsGrid} aria-label={`Drag ${card.title}`} className="cursor-grab" />
            </div>

            {card.deadline && card.stage === "saved" && <p className="text-xs text-tertiary">Deadline {formatDate(card.deadline)}</p>}
            {card.appliedAt && card.stage !== "saved" && <p className="text-xs text-tertiary">Applied {formatDate(card.appliedAt)}</p>}
            {card.stage === "interview" && (
                // shortcut: "just moved" = stage changed in the last 15 s (the card remounts in its new column, so no previous-stage state).
                <InterviewDate postingId={card.postingId} company={card.company} autoOpen={Date.now() - Date.parse(card.stageChangedAt) < 15_000} />
            )}

            {editing ? (
                <NotesEditor
                    initial={card.notes}
                    onCancel={() => setEditing(false)}
                    onSave={async (notes) => {
                        await onSaveNotes(card.id, notes);
                        setEditing(false);
                    }}
                />
            ) : (
                card.notes && <p className="line-clamp-3 text-sm whitespace-pre-line text-secondary">{card.notes}</p>
            )}

            {!editing && (
                <div className="flex flex-wrap items-center justify-between gap-2">
                    <MoveMenu card={card} onMove={onMove} />
                    <Button color="link-gray" size="sm" iconLeading={Edit03} onPress={() => setEditing(true)}>
                        {card.notes ? "Edit notes" : "Add notes"}
                    </Button>
                </div>
            )}
        </GridListItem>
    );
}

function MoveMenu({ card, onMove }: { card: Application; onMove: BoardProps["onMove"] }) {
    return (
        <MenuTrigger>
            <Button color="secondary" size="sm" iconLeading={ArrowsRight} aria-label={`Move ${card.title} to another column`}>
                Move to
            </Button>
            <Popover placement="bottom start" className="min-w-40 rounded-lg bg-primary py-1 shadow-lg ring-1 ring-secondary_alt">
                <Menu aria-label={`Move ${card.title} to`} onAction={(key) => onMove(card.id, key as Stage)} className="outline-hidden">
                    {stages.map((s) => (
                        <MenuItem
                            key={s.id}
                            id={s.id}
                            isDisabled={s.id === card.stage}
                            textValue={s.label}
                            className="cursor-pointer px-3 py-2 text-sm text-secondary outline-hidden data-[disabled]:cursor-default data-[disabled]:text-quaternary data-[focused]:bg-primary_hover"
                        >
                            {s.label}
                            {s.id === card.stage && <span className="sr-only"> (current)</span>}
                        </MenuItem>
                    ))}
                </Menu>
            </Popover>
        </MenuTrigger>
    );
}

function NotesEditor({ initial, onSave, onCancel }: { initial: string; onSave: (notes: string) => Promise<void>; onCancel: () => void }) {
    const [value, setValue] = useState(initial);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);

    async function save() {
        setSaving(true);
        setError(null);
        try {
            await onSave(value);
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Something went wrong. Try again.");
            setSaving(false);
        }
    }

    return (
        <div className="flex flex-col gap-2">
            <TextArea aria-label="Notes" rows={3} maxLength={2000} value={value} onChange={(e) => setValue(e.target.value)} placeholder="Contact, next step, how it went" />
            {error && (
                <p role="alert" className="text-sm text-error-primary">
                    {error}
                </p>
            )}
            <div className="flex gap-2">
                <Button size="sm" isLoading={saving} onPress={save}>
                    Save
                </Button>
                <Button size="sm" color="secondary" isDisabled={saving} onPress={onCancel}>
                    Cancel
                </Button>
            </div>
        </div>
    );
}

