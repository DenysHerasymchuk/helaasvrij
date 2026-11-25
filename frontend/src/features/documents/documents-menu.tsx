import { Dialog, DialogTrigger, Popover } from "react-aria-components";
import { File06, FolderClosed, Trash01 } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { itemsInCard } from "@/core/ui";
import { cx } from "@/utils/cx";

export type DocumentItem = { id: string; kind: string; language: "nl" | "fr" | "en"; title: string; createdAt: string; updatedAt: string };

const kindNames: Record<string, string> = { cv: "CV", coverLetter: "Cover letter", recruiterMessage: "Recruiter message" };
const languageNames: Record<string, string> = { nl: "Nederlands", fr: "Français", en: "English" };
const dateFmt = new Intl.DateTimeFormat(undefined, { dateStyle: "medium" });

/** Your saved documents, one click away from the preview instead of a panel that takes room from the forms. */
export function DocumentsMenu({ list, error, currentId, onOpen, onDelete, onRetry }: {
    list: DocumentItem[] | null;
    error: boolean;
    currentId: string | null;
    onOpen: (id: string) => void;
    onDelete: (doc: DocumentItem) => void;
    onRetry: () => void;
}) {
    return (
        <DialogTrigger>
            <Button size="sm" color="secondary" iconLeading={FolderClosed}>
                My documents{list ? ` (${list.length})` : ""}
            </Button>
            <Popover placement="bottom end" className="w-96 max-w-[calc(100vw-2rem)] rounded-xl bg-primary shadow-lg ring-1 ring-secondary_alt">
                <Dialog aria-label="My documents" className="max-h-96 overflow-y-auto outline-hidden">
                    {error ? (
                        <div className="flex flex-col items-start gap-3 p-4">
                            <p className="text-sm text-error-primary">Could not load your documents.</p>
                            <Button size="sm" color="secondary" onPress={onRetry}>
                                Try again
                            </Button>
                        </div>
                    ) : list === null ? (
                        <p role="status" className="p-4 text-sm text-tertiary">Loading...</p>
                    ) : list.length === 0 ? (
                        <p className="p-4 text-sm text-tertiary">No documents yet. Make your first one on the left.</p>
                    ) : (
                        <ul className={itemsInCard}>
                            {list.map((doc) => (
                                <li key={doc.id} className={cx("flex items-center gap-2 px-4 py-2.5", currentId === doc.id && "bg-secondary")}>
                                    <File06 className="size-5 shrink-0 text-fg-quaternary" aria-hidden="true" />
                                    <button type="button" onClick={() => onOpen(doc.id)} className="min-w-0 flex-1 cursor-pointer rounded-xs text-left outline-focus-ring focus-visible:outline-2" aria-label={`Open ${doc.title}`}>
                                        <span className="block truncate text-sm font-medium text-primary">{doc.title}</span>
                                        <span className="block text-xs text-tertiary">
                                            {kindNames[doc.kind] ?? doc.kind}, {languageNames[doc.language] ?? doc.language}, {dateFmt.format(new Date(doc.createdAt))}
                                        </span>
                                    </button>
                                    <Button size="sm" color="tertiary" iconLeading={Trash01} onPress={() => onDelete(doc)} aria-label={`Delete ${doc.title}`} />
                                </li>
                            ))}
                        </ul>
                    )}
                </Dialog>
            </Popover>
        </DialogTrigger>
    );
}
