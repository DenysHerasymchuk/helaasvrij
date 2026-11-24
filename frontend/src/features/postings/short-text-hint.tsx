import { useState } from "react";
import { AlertTriangle } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { TextArea } from "./fields";
import type { PostingDetail } from "./types";

/** Below this many characters (an Adzuna snippet, for example) the analysis often misses requirements. */
export const SHORT_POSTING_CHARS = 800;
const MIN_CHARS = 50;
const MAX_CHARS = 20000;

/** Warns about a short posting text and lets the student paste the full ad; the server re-reads the requirements. */
export function ShortTextHint({ posting, onUpdated }: { posting: PostingDetail; onUpdated: (posting: PostingDetail) => void }) {
    const [open, setOpen] = useState(false);
    const [text, setText] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    async function save() {
        setBusy(true);
        setError(null);
        try {
            const updated = await api.put<PostingDetail>(`/api/postings/${posting.id}/text`, { text });
            if (updated) {
                onUpdated(updated);
                setOpen(false);
                setText("");
            }
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Something went wrong. Try again.");
        } finally {
            setBusy(false);
        }
    }

    const tooShort = text.trim().length < MIN_CHARS;

    return (
        <section className="rounded-xl bg-warning-primary px-5 py-4 ring-1 ring-secondary">
            <div className="flex items-start gap-3">
                <AlertTriangle className="mt-0.5 size-5 shrink-0 text-fg-warning-primary" aria-hidden="true" />
                <div className="flex min-w-0 flex-1 flex-col gap-3">
                    <p className="text-sm text-primary">
                        This posting text is short, so the analysis can miss requirements. Paste the full ad text.
                        {posting.analysis.analysed && " Your current analysis is replaced, so analyse again after."}
                    </p>
                    {open ? (
                        <>
                            <label className="flex flex-col gap-1.5">
                                <span className="text-sm font-medium text-secondary">Full ad text</span>
                                <TextArea
                                    rows={8}
                                    maxLength={MAX_CHARS}
                                    value={text}
                                    disabled={busy}
                                    autoFocus
                                    onChange={(e) => setText(e.target.value)}
                                    placeholder="Open the original ad, copy all of its text and paste it here."
                                />
                            </label>
                            {error && (
                                <p role="alert" className="text-sm text-error-primary">
                                    {error}
                                </p>
                            )}
                            <div className="flex flex-wrap gap-3">
                                <Button size="md" onPress={save} isLoading={busy} isDisabled={busy || tooShort} showTextWhileLoading>
                                    {busy ? "Reading requirements..." : "Replace text"}
                                </Button>
                                <Button size="md" color="secondary" onPress={() => setOpen(false)} isDisabled={busy}>
                                    Cancel
                                </Button>
                            </div>
                        </>
                    ) : (
                        <div>
                            <Button size="sm" color="secondary" onPress={() => setOpen(true)}>
                                Paste the full ad text
                            </Button>
                        </div>
                    )}
                </div>
            </div>
        </section>
    );
}
