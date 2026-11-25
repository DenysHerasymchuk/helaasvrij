import { useEffect, useState } from "react";
import { Dialog, Heading, Modal, ModalOverlay } from "react-aria-components";
import { Button } from "@/components/base/buttons/button";
import { api } from "@/core/api";
import { SprintForm } from "./sprint-form";
import { type Sprint, type SprintDraft, newDraft } from "./types";
import { errorMessage } from "./use-sprints";

/** Starts a sprint. `count` is how many sprints exist, for the default name. */
export function StartSprintModal({ isOpen, count, onClose, onStarted }: { isOpen: boolean; count: number; onClose: () => void; onStarted: (sprint: Sprint) => void }) {
    const [draft, setDraft] = useState<SprintDraft>(() => newDraft(count + 1));
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (isOpen) {
            setDraft(newDraft(count + 1));
            setError(null);
        }
    }, [isOpen, count]);

    async function submit() {
        setBusy(true);
        setError(null);
        try {
            const sprint = await api.post<Sprint>("/api/sprints", draft);
            if (sprint) onStarted(sprint);
        } catch (e) {
            setError(errorMessage(e));
        } finally {
            setBusy(false);
        }
    }

    return (
        <ModalOverlay
            isOpen={isOpen}
            onOpenChange={(open) => !open && !busy && onClose()}
            isDismissable
            className="fixed inset-0 z-50 flex items-end justify-center bg-overlay/70 p-4 sm:items-center"
        >
            <Modal className="w-full max-w-lg">
                <Dialog className="flex flex-col gap-5 rounded-xl bg-primary p-6 shadow-xl outline-hidden ring-1 ring-secondary">
                    <div>
                        <Heading slot="title" className="text-lg font-semibold text-primary">
                            Start a sprint
                        </Heading>
                        <p className="mt-1 text-sm text-tertiary">A sprint is one round of applications. Pick a goal and a few weeks, then pull positions in from the backlog.</p>
                    </div>
                    <SprintForm draft={draft} onChange={setDraft} />
                    {error && (
                        <p role="alert" className="text-sm text-error-primary">
                            {error}
                        </p>
                    )}
                    <div className="flex justify-end gap-3">
                        <Button color="secondary" size="md" onPress={onClose} isDisabled={busy}>
                            Cancel
                        </Button>
                        <Button size="md" onPress={submit} isLoading={busy} isDisabled={!draft.name.trim()}>
                            Start sprint
                        </Button>
                    </div>
                </Dialog>
            </Modal>
        </ModalOverlay>
    );
}
