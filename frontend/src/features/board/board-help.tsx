import { Dialog, Heading, Modal, ModalOverlay } from "react-aria-components";
import { Button } from "@/components/base/buttons/button";

const keys: [string, string][] = [
    ["Arrow keys", "Move between cards"],
    ["Enter", "Open the card"],
    ["1 to 5", "Move the card to Saved, Applied, Interview, Offer or Rejected"],
    ["m", "Open the Move menu"],
    ["x", "Select or unselect the card"],
    ["/", "Search"],
    ["?", "Show this list"],
];

/** The keyboard shortcuts of the board. */
export function BoardHelp({ isOpen, onClose }: { isOpen: boolean; onClose: () => void }) {
    return (
        <ModalOverlay isOpen={isOpen} onOpenChange={(open) => !open && onClose()} isDismissable className="fixed inset-0 z-50 flex items-end justify-center bg-overlay/70 p-4 sm:items-center">
            <Modal className="w-full max-w-md">
                <Dialog className="flex flex-col gap-4 rounded-xl bg-primary p-6 shadow-xl outline-hidden ring-1 ring-secondary">
                    <Heading slot="title" className="text-lg font-semibold text-primary">
                        Keyboard shortcuts
                    </Heading>
                    <dl className="flex flex-col gap-2">
                        {keys.map(([key, what]) => (
                            <div key={key} className="flex items-center justify-between gap-4 text-sm">
                                <dt>
                                    <kbd className="rounded-md bg-secondary px-2 py-1 font-mono text-xs text-secondary ring-1 ring-secondary">{key}</kbd>
                                </dt>
                                <dd className="text-right text-tertiary">{what}</dd>
                            </div>
                        ))}
                    </dl>
                    <div className="flex justify-end">
                        <Button size="md" onPress={onClose}>
                            Done
                        </Button>
                    </div>
                </Dialog>
            </Modal>
        </ModalOverlay>
    );
}
