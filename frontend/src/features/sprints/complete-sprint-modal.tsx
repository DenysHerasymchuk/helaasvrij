import { useEffect, useState } from "react";
import { Dialog, Heading, Modal, ModalOverlay, Radio, RadioGroup } from "react-aria-components";
import { Button } from "@/components/base/buttons/button";
import { api } from "@/core/api";
import { SprintForm } from "./sprint-form";
import { type CompleteResult, type Sprint, type SprintDraft, newDraft } from "./types";
import { errorMessage } from "./use-sprints";

const radioClass =
    "flex cursor-pointer flex-col gap-0.5 rounded-lg p-3 ring-1 ring-secondary outline-focus-ring transition focus-visible:outline-2 selected:bg-brand-primary selected:ring-2 selected:ring-brand";

/** Ends a sprint. Open positions (saved, applied, interview) move into a new sprint or back to the backlog. */
export function CompleteSprintModal({ sprint, count, isOpen, onClose, onDone }: { sprint: Sprint; count: number; isOpen: boolean; onClose: () => void; onDone: (result: CompleteResult) => void }) {
    const [choice, setChoice] = useState<"next" | "backlog">("next");
    const [draft, setDraft] = useState<SprintDraft>(() => newDraft(count + 1));
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (isOpen) {
            setChoice(sprint.open > 0 ? "next" : "backlog");
            setDraft(newDraft(count + 1));
            setError(null);
        }
    }, [isOpen, count, sprint.open]);

    async function submit() {
        setBusy(true);
        setError(null);
        try {
            const result = await api.post<CompleteResult>(`/api/sprints/${sprint.id}/complete`, { next: choice === "next" ? draft : null });
            if (result) onDone(result);
        } catch (e) {
            setError(errorMessage(e));
        } finally {
            setBusy(false);
        }
    }

    const open = `${sprint.open} open ${sprint.open === 1 ? "position" : "positions"}`;

    return (
        <ModalOverlay
            isOpen={isOpen}
            onOpenChange={(o) => !o && !busy && onClose()}
            isDismissable
            className="fixed inset-0 z-50 flex items-end justify-center bg-overlay/70 p-4 sm:items-center"
        >
            <Modal className="w-full max-w-lg">
                <Dialog className="flex max-h-[90vh] flex-col gap-5 overflow-y-auto rounded-xl bg-primary p-6 shadow-xl outline-hidden ring-1 ring-secondary">
                    <div>
                        <Heading slot="title" className="text-lg font-semibold text-primary">
                            Complete {sprint.name}
                        </Heading>
                        <p className="mt-1 text-sm text-tertiary">
                            {sprint.applied} applied, {sprint.interviews} interviews, {sprint.offers} offers, {sprint.rejected} rejected. Offers and rejections stay in this sprint.
                        </p>
                    </div>

                    {sprint.open > 0 && (
                        <RadioGroup aria-label="What happens to open positions" value={choice} onChange={(v) => setChoice(v as "next" | "backlog")} className="flex flex-col gap-2">
                            <Radio value="next" className={radioClass}>
                                <span className="text-sm font-semibold text-primary">Start the next sprint with {open}</span>
                                <span className="text-sm text-tertiary">Saved, applied and interview positions carry over.</span>
                            </Radio>
                            <Radio value="backlog" className={radioClass}>
                                <span className="text-sm font-semibold text-primary">Move {open} to the backlog</span>
                                <span className="text-sm text-tertiary">No sprint is running until you start one.</span>
                            </Radio>
                        </RadioGroup>
                    )}

                    {choice === "next" && <SprintForm draft={draft} onChange={setDraft} />}

                    {error && (
                        <p role="alert" className="text-sm text-error-primary">
                            {error}
                        </p>
                    )}
                    <div className="flex justify-end gap-3">
                        <Button color="secondary" size="md" onPress={onClose} isDisabled={busy}>
                            Cancel
                        </Button>
                        <Button size="md" onPress={submit} isLoading={busy} isDisabled={choice === "next" && !draft.name.trim()}>
                            Complete sprint
                        </Button>
                    </div>
                </Dialog>
            </Modal>
        </ModalOverlay>
    );
}
