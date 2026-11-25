import { useState } from "react";
import { Dialog, Modal, ModalOverlay } from "react-aria-components";
import { useNavigate } from "react-router";
import { Button } from "@/components/base/buttons/button";
import { Input } from "@/components/base/input/input";
import { ApiError, api } from "@/core/api";
import { useAuth } from "@/core/auth";
import { SettingsCard, SettingsRow } from "./settings-row";

export function DataTab() {
    const [open, setOpen] = useState(false);
    const [typed, setTyped] = useState("");
    const [deleting, setDeleting] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const navigate = useNavigate();
    const { logout } = useAuth();

    const close = () => {
        if (deleting) return;
        setOpen(false);
        setTyped("");
        setError(null);
    };

    const confirmDelete = async () => {
        setDeleting(true);
        setError(null);
        try {
            await api.del("/api/me", { confirm: "DELETE" });
        } catch (caught) {
            setError(caught instanceof ApiError ? caught.message : "Something went wrong. Try again.");
            setDeleting(false);
            return;
        }
        // The server already ended the session; this clears the client state.
        await logout().catch(() => undefined);
        navigate("/login", { replace: true });
    };

    return (
        <SettingsCard tone="danger" title="Delete your data" description="Only you can see your data. Deleting removes all of it.">
            <SettingsRow title="Delete all my data" hint="Removes your profile, sources, postings, drafts, settings and account. This cannot be undone.">
                <Button color="primary-destructive" size="md" onClick={() => setOpen(true)}>
                    Delete all my data
                </Button>
            </SettingsRow>

            <ModalOverlay isOpen={open} onOpenChange={(next) => !next && close()} isDismissable={!deleting} className="fixed inset-0 z-50 flex items-end justify-center bg-overlay/70 p-4 sm:items-center">
                <Modal className="w-full max-w-md outline-hidden">
                    <Dialog aria-label="Delete all my data" className="flex flex-col gap-4 rounded-xl bg-primary p-6 shadow-xl outline-hidden">
                        <div>
                            <h2 className="text-lg font-semibold text-primary">Delete all my data?</h2>
                            <p className="mt-1 text-sm text-tertiary">Everything you own in helaasvrij is deleted and you are signed out. You cannot sign in with these credentials afterwards.</p>
                        </div>
                        <Input label="Type DELETE to confirm" size="md" value={typed} onChange={setTyped} isInvalid={!!error} hint={error ?? undefined} autoComplete="off" />
                        <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
                            <Button color="secondary" size="md" isDisabled={deleting} onClick={close}>
                                Cancel
                            </Button>
                            <Button color="primary-destructive" size="md" isDisabled={typed !== "DELETE"} isLoading={deleting} onClick={confirmDelete}>
                                Delete everything
                            </Button>
                        </div>
                    </Dialog>
                </Modal>
            </ModalOverlay>
        </SettingsCard>
    );
}
