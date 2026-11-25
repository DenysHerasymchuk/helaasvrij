import { Menu, MenuItem, MenuTrigger, Popover } from "react-aria-components";
import { AlarmClock, ArrowsRight, Calendar, Clock, DotsGrid, File05, Target04 } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { Checkbox } from "@/components/base/checkbox/checkbox";
import { type Stage, stages } from "@/features/applications/types";
import { ScoreRing } from "@/features/dashboard/score-ring";
import { formatDate, formatDateTime } from "@/features/postings/types";
import { cx } from "@/utils/cx";
import type { BoardItem } from "./use-board-data";
import { daysInStage, daysUntil } from "./view-options";

export const DRAG_TYPE = "application/x-board-cards";

const barColor: Record<Stage, string> = {
    saved: "bg-fg-quaternary",
    applied: "bg-fg-brand-primary_alt",
    interview: "bg-fg-warning-primary",
    offer: "bg-fg-success-primary",
    rejected: "bg-fg-error-primary",
};

const initials = (company: string) =>
    company
        .split(/\s+/)
        .filter(Boolean)
        .slice(0, 2)
        .map((w) => w[0]?.toUpperCase())
        .join("");

const chip = "inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-xs font-medium ring-1 ring-inset";

function Chip({ icon: Icon, tone = "gray", children }: { icon: typeof Clock; tone?: "gray" | "red" | "amber" | "blue"; children: React.ReactNode }) {
    const tones = {
        gray: "bg-secondary text-tertiary ring-secondary",
        red: "bg-error-primary text-error-primary ring-error_subtle",
        amber: "bg-warning-primary text-warning-primary ring-secondary",
        blue: "bg-brand-primary text-brand-secondary ring-brand",
    };
    return (
        <span className={cx(chip, tones[tone])}>
            <Icon className="size-3.5 shrink-0" aria-hidden="true" />
            {children}
        </span>
    );
}

export function MoveMenu({ item, onMove }: { item: BoardItem; onMove: (stage: Stage) => void }) {
    return (
        <MenuTrigger>
            <Button data-move color="secondary" size="sm" iconLeading={ArrowsRight} aria-label={`Move ${item.title} to another column`}>
                Move
            </Button>
            <Popover placement="bottom start" className="min-w-40 rounded-lg bg-primary py-1 shadow-lg ring-1 ring-secondary_alt">
                <Menu aria-label={`Move ${item.title} to`} onAction={(key) => onMove(key as Stage)} className="outline-hidden">
                    {stages.map((s, i) => (
                        <MenuItem
                            key={s.id}
                            id={s.id}
                            isDisabled={s.id === item.stage}
                            textValue={s.label}
                            className="flex cursor-pointer items-center justify-between gap-4 px-3 py-2 text-sm text-secondary outline-hidden data-[disabled]:cursor-default data-[disabled]:text-quaternary data-[focused]:bg-primary_hover"
                        >
                            <span>
                                {s.label}
                                {s.id === item.stage && <span className="sr-only"> (current)</span>}
                            </span>
                            <kbd className="text-xs text-quaternary">{i + 1}</kbd>
                        </MenuItem>
                    ))}
                </Menu>
            </Popover>
        </MenuTrigger>
    );
}

type Props = {
    item: BoardItem;
    selected: boolean;
    onSelect: () => void;
    onOpen: () => void;
    onMove: (stage: Stage) => void;
    /** Ids dragged with this card: the whole selection when this card is part of it. */
    dragIds: string[];
};

/** One position on the board: who, how well it matches, what is due, how long it has sat here. */
export function BoardCard({ item, selected, onSelect, onOpen, onMove, dragIds }: Props) {
    const left = item.deadline ? daysUntil(item.deadline) : null;
    const late = left !== null && left < 0;
    const urgent = left !== null && left <= 7 && item.stage === "saved";
    const days = daysInStage(item);

    return (
        <article
            draggable
            tabIndex={0}
            data-card-id={item.id}
            onDragStart={(e) => {
                e.dataTransfer.setData(DRAG_TYPE, JSON.stringify(dragIds));
                e.dataTransfer.effectAllowed = "move";
            }}
            aria-label={`${item.title} at ${item.company}, ${item.stage}`}
            className={cx(
                "group relative flex cursor-grab flex-col gap-2.5 overflow-hidden rounded-lg bg-primary p-3 pl-4 shadow-xs ring-1 outline-focus-ring transition focus-visible:outline-2 active:cursor-grabbing",
                selected ? "ring-2 ring-brand" : "ring-secondary hover:ring-secondary_alt",
            )}
        >
            <span aria-hidden="true" className={cx("absolute inset-y-0 left-0 w-1", barColor[item.stage])} />

            <div className="flex items-start gap-2.5">
                <span aria-hidden="true" className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-secondary text-sm font-semibold text-secondary ring-1 ring-secondary">
                    {initials(item.company)}
                </span>
                <div className="min-w-0 flex-1">
                    <p className="text-xs font-medium text-quaternary">{item.key}</p>
                    <button type="button" onClick={onOpen} className="block max-w-full cursor-pointer truncate rounded-xs text-left text-sm font-semibold text-primary outline-focus-ring hover:underline focus-visible:outline-2">
                        {item.title}
                    </button>
                    <p className="truncate text-sm text-tertiary">
                        {item.company}
                        {item.city && `, ${item.city}`}
                    </p>
                </div>
                {item.score !== null && <ScoreRing value={item.score} size={40} />}
            </div>

            <div className="flex flex-wrap gap-1.5">
                {item.deadline && (
                    <Chip icon={AlarmClock} tone={late ? "red" : urgent ? "amber" : "gray"}>
                        {late ? "Deadline passed" : left === 0 ? "Due today" : `Due ${formatDate(item.deadline)}`}
                    </Chip>
                )}
                {item.interviewAt && (
                    <Chip icon={Calendar} tone="blue">
                        {formatDateTime(item.interviewAt)}
                    </Chip>
                )}
                {item.openGaps > 0 && (
                    <Chip icon={Target04}>
                        {item.openGaps} open {item.openGaps === 1 ? "gap" : "gaps"}
                    </Chip>
                )}
                <Chip icon={Clock}>{days === 0 ? "Here today" : `${days}d here`}</Chip>
                {item.notes.trim() && (
                    <Chip icon={File05}>Notes</Chip>
                )}
            </div>

            <div className="flex items-center justify-between gap-2">
                <MoveMenu item={item} onMove={onMove} />
                <div className="flex items-center gap-2">
                    <Checkbox aria-label={`Select ${item.title}`} size="sm" isSelected={selected} onChange={onSelect} />
                    <DotsGrid className="size-4 shrink-0 text-fg-quaternary" aria-hidden="true" />
                </div>
            </div>
        </article>
    );
}
