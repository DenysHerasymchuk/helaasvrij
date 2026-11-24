import { type FormEvent, type ReactNode, useEffect, useState } from "react";
import { AlertCircle, Copy01, Edit05, Printer, Trash01 } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { cx } from "@/utils/cx";

type Language = "nl" | "fr" | "en";

interface DocumentResponse {
    id: string;
    kind: string;
    language: Language;
    title: string;
    content: string;
    createdAt: string;
    updatedAt: string;
    droppedCount: number | null;
}

type DocumentSummary = Omit<DocumentResponse, "content" | "droppedCount">;

const languageNames: Record<Language, string> = { nl: "Nederlands", fr: "Français", en: "English" };
const MAX_CHARS = 20000;

const fieldClass =
    "w-full rounded-lg bg-primary px-3.5 py-2.5 text-md text-primary shadow-xs ring-1 ring-primary ring-inset placeholder:text-placeholder outline-none focus:ring-2 focus:ring-brand aria-invalid:ring-error_subtle";

// Shows only the CV sheet when printing: the app shell (sidebar, header) and the rest of the page are hidden.
const printCss = `@media print {
  body * { visibility: hidden !important; }
  #cv-sheet, #cv-sheet * { visibility: visible !important; color: #000 !important; background: transparent !important; }
  #cv-sheet { position: absolute; left: 0; top: 0; width: 100%; box-shadow: none !important; padding: 0 !important; }
}`;

function Panel({ title, action, children, className }: { title: string; action?: ReactNode; children: ReactNode; className?: string }) {
    return (
        <section className={cx("rounded-xl bg-primary shadow-xs ring-1 ring-secondary", className)}>
            <header className="flex flex-wrap items-center justify-between gap-3 border-b border-secondary px-5 py-4">
                <h2 className="text-md font-semibold text-primary">{title}</h2>
                {action}
            </header>
            {children}
        </section>
    );
}

/** Renders the subset of markdown the CV uses: # and ## headings, - list items and paragraphs. Text only, no HTML. */
function Markdown({ source }: { source: string }) {
    const blocks: ReactNode[] = [];
    let list: string[] = [];
    const flush = () => {
        if (list.length === 0) return;
        blocks.push(
            <ul key={blocks.length} className="ml-5 list-disc space-y-1 text-sm text-secondary">
                {list.map((item, i) => (
                    <li key={i}>{item}</li>
                ))}
            </ul>,
        );
        list = [];
    };

    for (const raw of source.split("\n")) {
        const line = raw.trim();
        if (line.startsWith("- ")) {
            list.push(line.slice(2));
            continue;
        }
        flush();
        if (line.startsWith("## ")) blocks.push(<h3 key={blocks.length} className="pt-3 text-md font-semibold text-primary">{line.slice(3)}</h3>);
        else if (line.startsWith("# ")) blocks.push(<h2 key={blocks.length} className="text-xl font-semibold text-primary">{line.slice(2)}</h2>);
        else if (line) blocks.push(<p key={blocks.length} className="text-sm text-secondary">{line}</p>);
    }
    flush();
    return <div className="flex flex-col gap-2">{blocks}</div>;
}

function formatDate(iso: string) {
    return new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(new Date(iso));
}

export function DocumentsPage() {
    const [cvText, setCvText] = useState("");
    const [postingText, setPostingText] = useState("");
    const [language, setLanguage] = useState<"" | Language>("");
    const [tailoring, setTailoring] = useState(false);
    const [formError, setFormError] = useState<ApiError | null>(null);

    const [current, setCurrent] = useState<DocumentResponse | null>(null);
    const [editing, setEditing] = useState(false);
    const [draftTitle, setDraftTitle] = useState("");
    const [draftContent, setDraftContent] = useState("");
    const [saving, setSaving] = useState(false);
    const [saveError, setSaveError] = useState<string | null>(null);
    const [copied, setCopied] = useState(false);

    const [list, setList] = useState<DocumentSummary[] | null>(null);
    const [listError, setListError] = useState(false);

    const loadList = () => {
        setListError(false);
        api.get<DocumentSummary[]>("/api/documents")
            .then((docs) => setList(docs ?? []))
            .catch(() => setListError(true));
    };
    useEffect(loadList, []);

    const show = (doc: DocumentResponse) => {
        setCurrent(doc);
        setEditing(false);
        setSaveError(null);
    };

    const onTailor = async (event: FormEvent) => {
        event.preventDefault();
        setTailoring(true);
        setFormError(null);
        try {
            const doc = await api.post<DocumentResponse>("/api/documents/cv/tailor", { cvText, postingText, language: language || null });
            if (doc) {
                show(doc);
                loadList();
            }
        } catch (error) {
            setFormError(error instanceof ApiError ? error : new ApiError(0, "Something went wrong. Try again."));
        } finally {
            setTailoring(false);
        }
    };

    const onOpen = async (id: string) => {
        try {
            const doc = await api.get<DocumentResponse>(`/api/documents/${id}`);
            if (doc) show(doc);
        } catch {
            setListError(true);
        }
    };

    const onDelete = async (doc: DocumentSummary) => {
        if (!window.confirm(`Delete "${doc.title}"? This cannot be undone.`)) return;
        try {
            await api.del(`/api/documents/${doc.id}`);
            if (current?.id === doc.id) setCurrent(null);
            loadList();
        } catch {
            setListError(true);
        }
    };

    const onCopy = async () => {
        if (!current) return;
        await navigator.clipboard.writeText(current.content);
        setCopied(true);
        window.setTimeout(() => setCopied(false), 2000);
    };

    const onEdit = () => {
        if (!current) return;
        setDraftTitle(current.title);
        setDraftContent(current.content);
        setSaveError(null);
        setEditing(true);
    };

    const onSave = async () => {
        if (!current) return;
        setSaving(true);
        setSaveError(null);
        try {
            const doc = await api.put<DocumentResponse>(`/api/documents/${current.id}`, { title: draftTitle, content: draftContent });
            if (doc) {
                show({ ...doc, droppedCount: current.droppedCount });
                loadList();
            }
        } catch (error) {
            setSaveError(error instanceof ApiError ? error.message : "Could not save. Try again.");
        } finally {
            setSaving(false);
        }
    };

    const cvError = formError?.fieldErrors.cvText;
    const postingError = formError?.fieldErrors.postingText;
    const generalError = formError && !cvError && !postingError ? formError.message : null;

    return (
        <div className="mx-auto flex max-w-6xl flex-col gap-6 px-4 py-8 md:px-8">
            <style>{printCss}</style>
            <div className="flex flex-col gap-1">
                <h1 className="text-display-xs font-semibold text-primary">Documents</h1>
                <p className="text-md text-tertiary">Paste your CV and a job posting. You get your CV rewritten for that posting, using only what your CV already says.</p>
            </div>

            <div className="grid gap-6 lg:grid-cols-2">
                <Panel title="Tailor your CV">
                    <form onSubmit={onTailor} className="flex flex-col gap-5 p-5">
                        <div className="flex flex-col gap-1.5">
                            <label htmlFor="cv-text" className="text-sm font-medium text-secondary">Your CV</label>
                            <textarea
                                id="cv-text"
                                required
                                rows={10}
                                maxLength={MAX_CHARS}
                                value={cvText}
                                onChange={(e) => setCvText(e.target.value)}
                                placeholder="Paste your CV as plain text"
                                aria-invalid={!!cvError}
                                aria-describedby={cvError ? "cv-text-error" : undefined}
                                className={fieldClass}
                            />
                            {cvError && <p id="cv-text-error" className="text-sm text-error-primary">{cvError}</p>}
                        </div>

                        <div className="flex flex-col gap-1.5">
                            <label htmlFor="posting-text" className="text-sm font-medium text-secondary">Job posting</label>
                            <textarea
                                id="posting-text"
                                required
                                rows={8}
                                maxLength={MAX_CHARS}
                                value={postingText}
                                onChange={(e) => setPostingText(e.target.value)}
                                placeholder="Paste the full job posting"
                                aria-invalid={!!postingError}
                                aria-describedby={postingError ? "posting-text-error" : undefined}
                                className={fieldClass}
                            />
                            {postingError && <p id="posting-text-error" className="text-sm text-error-primary">{postingError}</p>}
                        </div>

                        <div className="flex flex-col gap-1.5">
                            <label htmlFor="cv-language" className="text-sm font-medium text-secondary">Language</label>
                            <select id="cv-language" value={language} onChange={(e) => setLanguage(e.target.value as "" | Language)} className={fieldClass}>
                                <option value="">Same as posting</option>
                                <option value="nl">Nederlands</option>
                                <option value="fr">Français</option>
                                <option value="en">English</option>
                            </select>
                        </div>

                        {generalError && (
                            <p role="alert" className="flex items-center gap-2 text-sm text-error-primary">
                                <AlertCircle className="size-4 shrink-0" aria-hidden="true" />
                                {generalError}
                            </p>
                        )}

                        <div>
                            <Button type="submit" size="md" isLoading={tailoring} isDisabled={tailoring} showTextWhileLoading>
                                {tailoring ? "Tailoring your CV..." : "Tailor CV"}
                            </Button>
                        </div>
                    </form>
                </Panel>

                <Panel
                    title={current ? current.title : "Tailored CV"}
                    action={
                        current && !editing ? (
                            <div className="flex flex-wrap gap-2">
                                <Button size="sm" color="secondary" iconLeading={Copy01} onPress={onCopy}>
                                    {copied ? "Copied" : "Copy"}
                                </Button>
                                <Button size="sm" color="secondary" iconLeading={Edit05} onPress={onEdit}>
                                    Edit
                                </Button>
                                <Button size="sm" color="secondary" iconLeading={Printer} onPress={() => window.print()}>
                                    Print / Save as PDF
                                </Button>
                            </div>
                        ) : undefined
                    }
                >
                    <div className="flex flex-col gap-4 p-5">
                        {!current && !tailoring && (
                            <p className="text-sm text-tertiary">Your tailored CV shows up here. Every line in it comes from your own CV.</p>
                        )}
                        {!current && tailoring && (
                            <p role="status" className="text-sm text-tertiary">Rewriting your CV for this posting. This takes up to a minute.</p>
                        )}

                        {current && !!current.droppedCount && (
                            <p className="rounded-lg bg-warning-primary px-3 py-2 text-sm text-warning-primary">
                                {current.droppedCount} {current.droppedCount === 1 ? "line was" : "lines were"} removed because they were not in your CV.
                            </p>
                        )}

                        {current && editing && (
                            <div className="flex flex-col gap-4">
                                <div className="flex flex-col gap-1.5">
                                    <label htmlFor="edit-title" className="text-sm font-medium text-secondary">Title</label>
                                    <input id="edit-title" value={draftTitle} maxLength={200} onChange={(e) => setDraftTitle(e.target.value)} className={fieldClass} />
                                </div>
                                <div className="flex flex-col gap-1.5">
                                    <label htmlFor="edit-content" className="text-sm font-medium text-secondary">Content (markdown)</label>
                                    <textarea id="edit-content" rows={18} value={draftContent} onChange={(e) => setDraftContent(e.target.value)} className={cx(fieldClass, "font-mono text-sm")} />
                                </div>
                                {saveError && <p role="alert" className="text-sm text-error-primary">{saveError}</p>}
                                <div className="flex gap-2">
                                    <Button size="md" onPress={onSave} isLoading={saving} isDisabled={saving} showTextWhileLoading>
                                        Save
                                    </Button>
                                    <Button size="md" color="secondary" onPress={() => setEditing(false)} isDisabled={saving}>
                                        Cancel
                                    </Button>
                                </div>
                            </div>
                        )}

                        {current && !editing && (
                            <article id="cv-sheet" className="rounded-lg bg-primary p-6 ring-1 ring-secondary">
                                <Markdown source={current.content} />
                            </article>
                        )}
                    </div>
                </Panel>
            </div>

            <Panel title="Your tailored CVs">
                {listError ? (
                    <div className="flex flex-col items-start gap-3 p-5">
                        <p className="text-sm text-error-primary">Could not load your CVs.</p>
                        <Button size="sm" color="secondary" onPress={loadList}>
                            Try again
                        </Button>
                    </div>
                ) : list === null ? (
                    <p role="status" className="p-5 text-sm text-tertiary">Loading...</p>
                ) : list.length === 0 ? (
                    <p className="p-5 text-sm text-tertiary">No tailored CVs yet. Fill in the form above to make your first one.</p>
                ) : (
                    <ul className="flex flex-col divide-y divide-secondary">
                        {list.map((doc) => (
                            <li key={doc.id} className="flex flex-wrap items-center gap-3 px-5 py-3">
                                <div className="min-w-0 flex-1">
                                    <p className="truncate text-sm font-medium text-primary">{doc.title}</p>
                                    <p className="text-sm text-tertiary">{formatDate(doc.createdAt)}</p>
                                </div>
                                <Badge size="sm" color="gray">{languageNames[doc.language] ?? doc.language}</Badge>
                                <Button size="sm" color="secondary" onPress={() => onOpen(doc.id)} aria-label={`Open ${doc.title}`}>
                                    Open
                                </Button>
                                <Button size="sm" color="tertiary" iconLeading={Trash01} onPress={() => onDelete(doc)} aria-label={`Delete ${doc.title}`} />
                            </li>
                        ))}
                    </ul>
                )}
            </Panel>
        </div>
    );
}
