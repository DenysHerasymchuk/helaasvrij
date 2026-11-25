import { useEffect, useState } from "react";
import { Dialog, Heading, Modal, ModalOverlay } from "react-aria-components";
import { XClose } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { api } from "@/core/api";
import type { Sprint } from "@/features/sprints/types";
import { errorMessage } from "@/features/sprints/use-sprints";

type Point = { date: string; scope: number; open: number };

const pct = (part: number, whole: number) => (whole === 0 ? "none yet" : `${Math.round((part / whole) * 100)}%`);

function Burndown({ points }: { points: Point[] }) {
    const w = 460;
    const h = 150;
    const pad = 24;
    const max = Math.max(1, ...points.map((p) => p.scope));
    const x = (i: number) => pad + (points.length <= 1 ? 0 : (i / (points.length - 1)) * (w - pad * 2));
    const y = (v: number) => h - pad - (v / max) * (h - pad * 2);
    const line = (pick: (p: Point) => number) => points.map((p, i) => `${i === 0 ? "M" : "L"}${x(i).toFixed(1)},${y(pick(p)).toFixed(1)}`).join(" ");

    return (
        <figure className="flex flex-col gap-2">
            <svg viewBox={`0 0 ${w} ${h}`} role="img" aria-label="Positions in the sprint and positions still open, per day" className="w-full">
                <line x1={pad} x2={w - pad} y1={h - pad} y2={h - pad} stroke="var(--color-border-secondary)" />
                <path d={line((p) => p.scope)} fill="none" stroke="var(--color-fg-quaternary)" strokeWidth="2" strokeDasharray="4 4" />
                <path d={line((p) => p.open)} fill="none" stroke="var(--color-fg-brand-primary_alt)" strokeWidth="2.5" strokeLinejoin="round" />
                <text x={pad} y={h - 6} className="fill-current text-[10px] text-tertiary">
                    {points[0]?.date.slice(5)}
                </text>
                <text x={w - pad} y={h - 6} textAnchor="end" className="fill-current text-[10px] text-tertiary">
                    {points[points.length - 1]?.date.slice(5)}
                </text>
                <text x={4} y={y(max) + 4} className="fill-current text-[10px] text-tertiary">
                    {max}
                </text>
            </svg>
            <figcaption className="flex gap-4 text-xs text-tertiary">
                <span className="flex items-center gap-1.5">
                    <span className="inline-block h-0.5 w-5 bg-fg-brand-primary_alt" /> Still open
                </span>
                <span className="flex items-center gap-1.5">
                    <span className="inline-block h-0.5 w-5 border-t-2 border-dashed border-fg-quaternary" /> In the sprint
                </span>
            </figcaption>
        </figure>
    );
}

/** The sprint in numbers: where positions got to, how they convert, and how the open pile shrank day by day. */
export function BoardInsights({ sprint, isOpen, onClose }: { sprint: Sprint; isOpen: boolean; onClose: () => void }) {
    const [points, setPoints] = useState<Point[] | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (!isOpen) return;
        let alive = true;
        setError(null);
        api.get<Point[]>(`/api/sprints/${sprint.id}/burndown`)
            .then((p) => alive && setPoints(p ?? []))
            .catch((e) => alive && setError(errorMessage(e)));
        return () => {
            alive = false;
        };
    }, [isOpen, sprint.id, sprint.items, sprint.applied, sprint.offers, sprint.rejected]);

    const funnel = [
        { label: "In the sprint", n: sprint.items },
        { label: "Applied", n: sprint.applied },
        { label: "Interview", n: sprint.interviews },
        { label: "Offer", n: sprint.offers },
    ];
    const top = Math.max(1, sprint.items);

    return (
        <ModalOverlay isOpen={isOpen} onOpenChange={(open) => !open && onClose()} isDismissable className="fixed inset-0 z-50 flex items-end justify-center bg-overlay/70 p-4 sm:items-center">
            <Modal className="w-full max-w-xl">
                <Dialog className="flex max-h-[90vh] flex-col gap-5 overflow-y-auto rounded-xl bg-primary p-6 shadow-xl outline-hidden ring-1 ring-secondary">
                    <div className="flex items-start justify-between gap-4">
                        <div>
                            <Heading slot="title" className="text-lg font-semibold text-primary">
                                {sprint.name} in numbers
                            </Heading>
                            <p className="mt-1 text-sm text-tertiary">{sprint.goal}</p>
                        </div>
                        <Button size="sm" color="tertiary" iconLeading={XClose} aria-label="Close" onPress={onClose} />
                    </div>

                    <section aria-label="Funnel" className="flex flex-col gap-2">
                        <h3 className="text-sm font-semibold text-primary">Where positions got to</h3>
                        {funnel.map((f) => (
                            <div key={f.label} className="grid grid-cols-[6.5rem_1fr_2rem] items-center gap-3 text-sm">
                                <span className="text-secondary">{f.label}</span>
                                <span className="h-2.5 overflow-hidden rounded-full bg-secondary">
                                    <span className="block h-full rounded-full bg-fg-brand-primary_alt" style={{ width: `${(f.n / top) * 100}%` }} />
                                </span>
                                <span className="text-right font-semibold text-primary">{f.n}</span>
                            </div>
                        ))}
                        <p className="text-sm text-tertiary">
                            {pct(sprint.applied, sprint.items)} applied, {pct(sprint.interviews, sprint.applied)} of those reached an interview, {pct(sprint.offers, sprint.interviews)} of interviews gave an offer. {sprint.rejected}{" "}
                            {sprint.rejected === 1 ? "position was" : "positions were"} rejected.
                        </p>
                    </section>

                    <section aria-label="Burndown" className="flex flex-col gap-2">
                        <h3 className="text-sm font-semibold text-primary">Open positions per day</h3>
                        {error ? (
                            <p role="alert" className="text-sm text-error-primary">
                                {error}
                            </p>
                        ) : points === null ? (
                            <p role="status" className="text-sm text-tertiary">
                                Loading...
                            </p>
                        ) : points.length === 0 ? (
                            <p className="text-sm text-tertiary">The sprint has not started yet.</p>
                        ) : (
                            <Burndown points={points} />
                        )}
                    </section>
                </Dialog>
            </Modal>
        </ModalOverlay>
    );
}
