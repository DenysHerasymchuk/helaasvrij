import { useEffect, useState } from "react";
import { Dialog, Heading, Modal, ModalOverlay } from "react-aria-components";
import { Link } from "react-router";
import { File06, MessageChatCircle, Target04, XClose } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { api } from "@/core/api";
import { InterviewDate } from "@/features/calendar/interview-date";
import { type Stage, stages } from "@/features/applications/types";
import { ScoreRing } from "@/features/dashboard/score-ring";
import { ActivityPanel } from "@/features/postings/activity";
import { TextArea } from "@/features/postings/fields";
import { formatDate } from "@/features/postings/types";
import { errorMessage } from "@/features/sprints/use-sprints";
import { cx } from "@/utils/cx";
import type { BoardItem } from "./use-board-data";

type Gap = { requirementId: string; postingId: string; requirement: string; action: string | null; done: boolean; scoreGain: number };

const sectionTitle = "text-sm font-semibold text-primary";

type Props = {
    item: BoardItem | null;
    onClose: () => void;
    onMove: (stage: Stage) => void;
    onSaveNotes: (notes: string) => Promise<void>;
    onRemove: () => void;
};

/** Everything about one card in a side panel, so you can work on it without leaving the board. */
export function BoardDrawer({ item, onClose, onMove, onSaveNotes, onRemove }: Props) {
    const [notes, setNotes] = useState("");
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [gaps, setGaps] = useState<Gap[] | null>(null);
    const id = item?.id;
    const postingId = item?.postingId;

    useEffect(() => {
        setNotes(item?.notes ?? "");
        setError(null);
        // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when another card opens
    }, [id]);

    useEffect(() => {
        if (!postingId) return;
        let alive = true;
        setGaps(null);
        api.get<Gap[]>("/api/gaps")
            .then((all) => alive && setGaps((all ?? []).filter((g) => g.postingId === postingId && !g.done)))
            .catch(() => alive && setGaps([]));
        return () => {
            alive = false;
        };
    }, [postingId]);

    async function save() {
        setSaving(true);
        setError(null);
        try {
            await onSaveNotes(notes);
        } catch (e) {
            setError(errorMessage(e));
        } finally {
            setSaving(false);
        }
    }

    return (
        <ModalOverlay isOpen={!!item} onOpenChange={(open) => !open && onClose()} isDismissable className="fixed inset-0 z-50 flex justify-end bg-overlay/60">
            <Modal className="h-full w-full max-w-lg">
                <Dialog aria-label={item ? `${item.title} at ${item.company}` : "Position"} className="flex h-full flex-col overflow-y-auto bg-primary shadow-xl outline-hidden ring-1 ring-secondary">
                    {item && (
                        <>
                            <header className="sticky top-0 z-10 flex items-start gap-3 border-b border-secondary bg-primary px-5 py-4">
                                <div className="min-w-0 flex-1">
                                    <p className="text-xs font-medium text-quaternary">{item.key}</p>
                                    <Heading slot="title" className="text-lg font-semibold text-primary">
                                        {item.title}
                                    </Heading>
                                    <p className="text-sm text-tertiary">
                                        {item.company}
                                        {item.city && `, ${item.city}`}
                                    </p>
                                </div>
                                {item.score !== null && <ScoreRing value={item.score} size={48} />}
                                <Button size="sm" color="tertiary" iconLeading={XClose} aria-label="Close" onPress={onClose} />
                            </header>

                            <div className="flex flex-col gap-6 px-5 py-5">
                                <section aria-label="Stage" className="flex flex-col gap-2">
                                    <h3 className={sectionTitle}>Stage</h3>
                                    <div role="group" aria-label="Move to" className="grid grid-cols-5 gap-1 rounded-lg bg-secondary p-1">
                                        {stages.map((s) => (
                                            <button
                                                key={s.id}
                                                type="button"
                                                aria-pressed={item.stage === s.id}
                                                onClick={() => item.stage !== s.id && onMove(s.id)}
                                                className={cx(
                                                    "cursor-pointer rounded-md px-1 py-1.5 text-xs font-semibold outline-focus-ring transition focus-visible:outline-2",
                                                    item.stage === s.id ? "bg-primary text-secondary shadow-xs" : "text-quaternary hover:text-secondary",
                                                )}
                                            >
                                                {s.label}
                                            </button>
                                        ))}
                                    </div>
                                </section>

                                <section aria-label="Dates" className="flex flex-col gap-2">
                                    <h3 className={sectionTitle}>Dates</h3>
                                    <p className="text-sm text-tertiary">{item.deadline ? `Deadline ${formatDate(item.deadline)}` : "No deadline set."}</p>
                                    {item.stage === "interview" && <InterviewDate postingId={item.postingId} company={item.company} />}
                                </section>

                                <section aria-label="Open gaps" className="flex flex-col gap-2">
                                    <h3 className={sectionTitle}>What you still miss</h3>
                                    {gaps === null ? (
                                        <p role="status" className="text-sm text-tertiary">
                                            Loading...
                                        </p>
                                    ) : gaps.length === 0 ? (
                                        <p className="text-sm text-tertiary">{item.score === null ? "Check this position against your profile to see its gaps." : "No open gaps."}</p>
                                    ) : (
                                        <ul className="flex flex-col gap-2">
                                            {gaps.slice(0, 4).map((g) => (
                                                <li key={g.requirementId} className="rounded-lg px-3 py-2 text-sm ring-1 ring-secondary">
                                                    <p className="font-medium text-primary">{g.action ?? g.requirement}</p>
                                                    {g.scoreGain > 0 && <p className="text-xs text-tertiary">Up to +{g.scoreGain}% match</p>}
                                                </li>
                                            ))}
                                        </ul>
                                    )}
                                </section>

                                <section aria-label="Notes" className="flex flex-col gap-2">
                                    <h3 className={sectionTitle}>Notes</h3>
                                    <TextArea aria-label="Notes" rows={4} maxLength={2000} value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Contact, next step, how it went" />
                                    {error && (
                                        <p role="alert" className="text-sm text-error-primary">
                                            {error}
                                        </p>
                                    )}
                                    <div>
                                        <Button size="sm" onPress={save} isLoading={saving} isDisabled={notes === item.notes}>
                                            Save notes
                                        </Button>
                                    </div>
                                </section>

                                <section aria-label="Work on this position" className="flex flex-wrap gap-2">
                                    <Button size="sm" color="secondary" iconLeading={Target04} href={`/gaps?postingId=${item.postingId}`}>
                                        Gap plan
                                    </Button>
                                    <Button size="sm" color="secondary" iconLeading={File06} href={`/documents?postingId=${item.postingId}&mode=tailor`}>
                                        Tailor CV
                                    </Button>
                                    <Button size="sm" color="secondary" iconLeading={MessageChatCircle} href={`/interview?postingId=${item.postingId}`}>
                                        Interview prep
                                    </Button>
                                    <Link to={`/positions/${item.postingId}`} className="self-center rounded-xs text-sm font-semibold text-brand-secondary outline-focus-ring hover:underline focus-visible:outline-2">
                                        Open the position
                                    </Link>
                                </section>

                                <ActivityPanel applicationId={item.id} version={`${item.stage}:${item.sprintId}`} />

                                {item.stage === "saved" && (
                                    <div>
                                        <Button size="sm" color="secondary-destructive" onPress={onRemove}>
                                            Remove from sprint
                                        </Button>
                                    </div>
                                )}
                            </div>
                        </>
                    )}
                </Dialog>
            </Modal>
        </ModalOverlay>
    );
}
