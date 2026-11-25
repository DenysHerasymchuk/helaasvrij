import { useState } from "react";
import { Dialog, Heading, Modal, ModalOverlay, Tab, TabList, TabPanel, Tabs } from "react-aria-components";
import { XClose } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { Input } from "@/components/base/input/input";
import { ApiError, api } from "@/core/api";
import { Field, TextArea } from "./fields";
import type { PostingDetail } from "./types";

type Mode = "paste" | "url";

const tabClass =
    "cursor-pointer rounded-md px-3 py-1.5 text-sm font-semibold text-quaternary outline-focus-ring transition selected:bg-active selected:text-secondary hover:text-secondary focus-visible:outline-2";

export function AddPostingModal({ isOpen, onClose, onAdded }: { isOpen: boolean; onClose: () => void; onAdded: (posting: PostingDetail) => void }) {
    const [mode, setMode] = useState<Mode>("paste");
    const [text, setText] = useState("");
    const [link, setLink] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // A 422 from the URL flow means "we could not read it": move the user to the paste tab.
    const [pasteHint, setPasteHint] = useState(false);

    async function submit() {
        setBusy(true);
        setError(null);
        try {
            const posting =
                mode === "paste"
                    ? await api.post<PostingDetail>("/api/postings/paste", { text, url: link.trim() || undefined })
                    : await api.post<PostingDetail>("/api/postings/url", { url: link });
            onAdded(posting!);
        } catch (e) {
            const err = e as ApiError;
            setError(err instanceof ApiError ? err.message : "Something went wrong. Try again.");
            if (mode === "url" && err instanceof ApiError && err.status === 422) {
                setMode("paste");
                setPasteHint(true);
            }
        } finally {
            setBusy(false);
        }
    }

    const canSubmit = mode === "paste" ? text.trim().length > 0 : link.trim().length > 0;

    return (
        <ModalOverlay
            isOpen={isOpen}
            onOpenChange={(open) => !open && !busy && onClose()}
            isDismissable
            className="fixed inset-0 z-50 flex items-end justify-center bg-overlay/70 p-4 sm:items-center"
        >
            <Modal className="w-full max-w-xl">
                <Dialog className="flex flex-col gap-5 rounded-xl bg-primary p-6 shadow-xl outline-hidden ring-1 ring-secondary">
                    <div className="flex items-start justify-between gap-4">
                        <div>
                            <Heading slot="title" className="text-lg font-semibold text-primary">
                                Add posting
                            </Heading>
                            <p className="mt-1 text-sm text-tertiary">Paste the text or add a link. We read the requirements and the language for you.</p>
                        </div>
                        <Button size="sm" color="tertiary" iconLeading={XClose} aria-label="Close" onPress={onClose} isDisabled={busy} />
                    </div>

                    <Tabs selectedKey={mode} onSelectionChange={(key) => { setMode(key as Mode); setError(null); }}>
                        <TabList aria-label="How to add" className="mb-4 inline-flex gap-1 rounded-lg bg-secondary p-1">
                            <Tab id="paste" className={tabClass}>
                                Paste text
                            </Tab>
                            <Tab id="url" className={tabClass}>
                                From URL
                            </Tab>
                        </TabList>

                        <TabPanel id="paste" className="flex flex-col gap-4 outline-hidden">
                            {pasteHint && (
                                <p className="text-sm text-tertiary">Copy the posting from the page and paste it here instead.</p>
                            )}
                            <Field label="Posting text">
                                <TextArea rows={9} value={text} onChange={(e) => setText(e.target.value)} placeholder="Paste the full posting" />
                            </Field>
                            <Input label="Link (optional)" size="md" placeholder="https://" value={link} onChange={setLink} />
                        </TabPanel>
                        <TabPanel id="url" className="flex flex-col gap-4 outline-hidden">
                            <Input
                                label="Posting link"
                                size="md"
                                placeholder="https://company.be/jobs/intern"
                                value={link}
                                onChange={setLink}
                                hint="Works for pages with structured job data. LinkedIn and JobTeaser links are not supported."
                            />
                        </TabPanel>
                    </Tabs>

                    {error && (
                        <p role="alert" className="text-sm text-error-primary">
                            {error}
                        </p>
                    )}

                    <div className="flex justify-end gap-3">
                        <Button color="secondary" size="md" onPress={onClose} isDisabled={busy}>
                            Cancel
                        </Button>
                        <Button size="md" onPress={submit} isLoading={busy} isDisabled={!canSubmit}>
                            Add posting
                        </Button>
                    </div>
                </Dialog>
            </Modal>
        </ModalOverlay>
    );
}
