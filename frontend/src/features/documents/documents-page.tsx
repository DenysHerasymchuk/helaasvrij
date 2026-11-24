import { type FormEvent, type ReactNode, useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { AlertCircle, Copy01, Download01, Edit05, Printer, Trash01 } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { InputFile } from "@/components/base/input/input-file";
import { RadioButton, RadioGroup } from "@/components/base/radio-buttons/radio-buttons";
import { ApiError, api } from "@/core/api";
import type { PostingSummary } from "@/features/postings/types";
import { getProfile } from "@/features/profile/api";
import { cx } from "@/utils/cx";
import { fieldClass, PageHeader } from "@/core/ui";

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


type DraftKind = "coverLetter" | "recruiterMessage";

const kindNames: Record<string, string> = { cv: "CV", coverLetter: "Cover letter", recruiterMessage: "Recruiter message" };
const RECRUITER_MAX = 300;

const languageNames: Record<Language, string> = { nl: "Nederlands", fr: "Français", en: "English" };
const MAX_CHARS = 20000;


// Shows only the CV sheet when printing: the app shell (sidebar, header) and the rest of the page are hidden.
const printCss = `@media print {
  body * { visibility: hidden !important; }
  #cv-sheet, #cv-sheet * { visibility: visible !important; color: #000 !important; background: transparent !important; }
  #cv-sheet { position: absolute; left: 0; top: 0; width: 100%; box-shadow: none !important; padding: 0 !important; }
}`;

function Panel({ id, title, action, children, className }: { id?: string; title: string; action?: ReactNode; children: ReactNode; className?: string }) {
    return (
        <section id={id} className={cx("scroll-mt-6 rounded-xl bg-primary shadow-xs ring-1 ring-secondary", className)}>
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

/** Reads a problem title from a failed response, or falls back to a generic message. */
async function problemTitle(response: Response, fallback: string) {
    try {
        const problem = await response.json();
        if (typeof problem.title === "string") return problem.title as string;
    } catch {
        // Not JSON: keep the fallback.
    }
    return fallback;
}

/** The upload is multipart, so it bypasses the JSON-only `api` helper. Only the extracted text comes back; the file is not kept. */
async function extractCvText(file: File): Promise<string> {
    const form = new FormData();
    form.append("file", file);
    const response = await fetch("/api/documents/cv/extract", { method: "POST", credentials: "same-origin", body: form });
    if (response.ok) return ((await response.json()) as { text: string }).text;
    throw new ApiError(response.status, await problemTitle(response, "Could not read this file. Try again."));
}

async function downloadDocx(id: string, title: string) {
    const response = await fetch(`/api/documents/${id}/docx`, { credentials: "same-origin" });
    if (!response.ok) throw new ApiError(response.status, await problemTitle(response, "Could not download. Try again."));
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement("a");
    link.href = url;
    link.download = `${title}.docx`;
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

const MAX_FILE_BYTES = 5 * 1024 * 1024;

function formatDate(iso: string) {
    return new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(new Date(iso));
}

export function DocumentsPage() {
    // Deep links from a posting: ?postingId=&mode=tailor|create or ?postingId=&kind=coverLetter|recruiterMessage.
    const [params] = useSearchParams();
    const linkedPosting = params.get("postingId") ?? "";
    const linkedKind = params.get("kind");
    const linkedMode = params.get("mode");
    const [cvSource, setCvSource] = useState<"paste" | "upload">("paste");
    const [cvText, setCvText] = useState("");
    const [cvFile, setCvFile] = useState<File | null>(null);
    const [extracting, setExtracting] = useState(false);
    const [extractError, setExtractError] = useState<string | null>(null);

    const [postingSource, setPostingSource] = useState<"saved" | "paste">("paste");
    const [postingText, setPostingText] = useState("");
    const [postingId, setPostingId] = useState(linkedPosting);
    const [postings, setPostings] = useState<PostingSummary[] | null>(null);
    const [postingsError, setPostingsError] = useState(false);
    const [downloading, setDownloading] = useState(false);
    const [downloadError, setDownloadError] = useState<string | null>(null);
    const [language, setLanguage] = useState<"" | Language>("");
    const [tailoring, setTailoring] = useState(false);
    const [formError, setFormError] = useState<ApiError | null>(null);

    // null = loading, -1 = could not load
    const [confirmedCount, setConfirmedCount] = useState<number | null>(null);
    const [createLanguage, setCreateLanguage] = useState<"" | Language>("");
    const [creating, setCreating] = useState(false);
    const [createError, setCreateError] = useState<string | null>(null);

    const [current, setCurrent] = useState<DocumentResponse | null>(null);
    const [editing, setEditing] = useState(false);
    const [draftTitle, setDraftTitle] = useState("");
    const [draftContent, setDraftContent] = useState("");
    const [saving, setSaving] = useState(false);
    const [saveError, setSaveError] = useState<string | null>(null);
    const [copied, setCopied] = useState(false);

    const [draftPostingId, setDraftPostingId] = useState(linkedPosting);
    const [draftKind, setDraftKind] = useState<DraftKind>(linkedKind === "recruiterMessage" ? "recruiterMessage" : "coverLetter");
    const [draftLanguage, setDraftLanguage] = useState<"" | Language>("");
    const [drafting, setDrafting] = useState(false);
    const [draftError, setDraftError] = useState<string | null>(null);

    const [list, setList] = useState<DocumentSummary[] | null>(null);
    const [listError, setListError] = useState(false);

    const loadList = () => {
        setListError(false);
        api.get<DocumentSummary[]>("/api/documents")
            .then((docs) => setList(docs ?? []))
            .catch(() => setListError(true));
    };
    useEffect(loadList, []);

    const loadPostings = () => {
        setPostingsError(false);
        setPostings(null);
        api.get<PostingSummary[]>("/api/postings")
            .then((items) => {
                setPostings(items ?? []);
                if (items && items.length > 0) setPostingSource("saved");
            })
            .catch(() => setPostingsError(true));
    };
    useEffect(loadPostings, []);

    // Scroll once the postings are in, so the panel sits where it will stay.
    const postingsLoaded = postings !== null;
    useEffect(() => {
        if (!postingsLoaded) return;
        const target =
            linkedKind === "coverLetter" || linkedKind === "recruiterMessage"
                ? "draft-panel"
                : linkedMode === "create"
                  ? "create-panel"
                  : linkedMode === "tailor" || linkedPosting
                    ? "tailor-panel"
                    : null;
        if (target) document.getElementById(target)?.scrollIntoView({ behavior: "smooth", block: "start" });
    }, [postingsLoaded, linkedKind, linkedMode, linkedPosting]);

    const loadProfile = () => {
        setConfirmedCount(null);
        getProfile()
            .then((profile) => setConfirmedCount(profile?.items.filter((item) => item.confirmed).length ?? 0))
            .catch(() => setConfirmedCount(-1));
    };
    useEffect(loadProfile, []);

    const onCreate = async (event: FormEvent) => {
        event.preventDefault();
        setCreating(true);
        setCreateError(null);
        try {
            const doc = await api.post<DocumentResponse>("/api/documents/cv/create", { language: createLanguage || null });
            if (doc) {
                show(doc);
                loadList();
            }
        } catch (error) {
            // The profile changed in another tab: show the empty state again.
            if (error instanceof ApiError && error.status === 400 && error.message.startsWith("Confirm some profile items")) loadProfile();
            else setCreateError(error instanceof ApiError ? error.message : "Something went wrong. Try again.");
        } finally {
            setCreating(false);
        }
    };

    const onExtract = async (file: File | null) => {
        setCvFile(file);
        setExtractError(null);
        if (!file) return;
        if (file.size > MAX_FILE_BYTES) {
            setExtractError("The file is larger than 5 MB.");
            return;
        }
        setExtracting(true);
        try {
            setCvText(await extractCvText(file));
        } catch (error) {
            setExtractError(error instanceof ApiError ? error.message : "Could not read this file. Try again.");
        } finally {
            setExtracting(false);
        }
    };

    const onDownload = async () => {
        if (!current) return;
        setDownloading(true);
        setDownloadError(null);
        try {
            await downloadDocx(current.id, current.title);
        } catch (error) {
            setDownloadError(error instanceof ApiError ? error.message : "Could not download. Try again.");
        } finally {
            setDownloading(false);
        }
    };

    const onDraft = async (event: FormEvent) => {
        event.preventDefault();
        if (!draftPostingId) return;
        setDrafting(true);
        setDraftError(null);
        try {
            const doc = await api.post<DocumentResponse>("/api/documents/draft", {
                postingId: draftPostingId,
                kind: draftKind,
                language: draftLanguage || null,
            });
            if (doc) {
                show(doc);
                loadList();
            }
        } catch (error) {
            setDraftError(error instanceof ApiError ? error.message : "Something went wrong. Try again.");
        } finally {
            setDrafting(false);
        }
    };

    const show = (doc: DocumentResponse) => {
        setCurrent(doc);
        setEditing(false);
        setSaveError(null);
        setDownloadError(null);
    };

    const onTailor = async (event: FormEvent) => {
        event.preventDefault();
        setTailoring(true);
        setFormError(null);
        try {
            const posting = postingSource === "saved" ? { postingId } : { postingText };
            const doc = await api.post<DocumentResponse>("/api/documents/cv/tailor", { cvText, ...posting, language: language || null });
            if (doc) {
                show(doc);
                loadList();
            }
        } catch (error) {
            if (error instanceof ApiError && error.status === 404 && postingSource === "saved") {
                setFormError(new ApiError(404, "", { postingId: "This posting no longer exists. Pick another one." }));
                setPostingId("");
                loadPostings();
            } else {
                setFormError(error instanceof ApiError ? error : new ApiError(0, "Something went wrong. Try again."));
            }
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
    const postingError = formError?.fieldErrors.postingText ?? formError?.fieldErrors.postingId;
    const generalError = formError && !cvError && !postingError ? formError.message : null;
    const showCvText = cvSource === "paste" || cvText.length > 0;

    return (
        <div className="flex max-w-6xl flex-col gap-6 px-4 py-8 md:px-8">
            <style>{printCss}</style>
            <PageHeader
                title="Documents"
                description="No CV yet? Create one from your confirmed profile. Already have one? Tailor it to a job posting. Both use only facts you gave us."
            />

            <div className="grid items-start gap-6 lg:grid-cols-2">
                <div className="flex flex-col gap-6">
                    <Panel id="create-panel" title="Create my CV from my profile">
                        {confirmedCount === null ? (
                            <p role="status" className="p-5 text-sm text-tertiary">Loading your profile...</p>
                        ) : confirmedCount === -1 ? (
                            <div role="alert" className="flex flex-wrap items-center gap-3 p-5">
                                <p className="text-sm text-error-primary">Could not load your profile.</p>
                                <Button size="sm" color="secondary" onPress={loadProfile}>
                                    Try again
                                </Button>
                            </div>
                        ) : confirmedCount === 0 ? (
                            <div className="flex flex-col items-start gap-3 p-5">
                                <p className="text-sm text-tertiary">
                                    Your CV is built only from profile items you confirmed, and you have none yet. Add your GitHub, CV or website and confirm what is right.
                                </p>
                                <Button size="sm" color="secondary" href="/profile">
                                    Go to my profile
                                </Button>
                            </div>
                        ) : (
                            <form onSubmit={onCreate} className="flex flex-col gap-4 p-5">
                                <p className="text-sm text-tertiary">
                                    Uses your {confirmedCount} confirmed profile {confirmedCount === 1 ? "item" : "items"}. Nothing else goes into it.
                                </p>
                                <div className="flex flex-col gap-1.5">
                                    <label htmlFor="create-language" className="text-sm font-medium text-secondary">Language</label>
                                    <select
                                        id="create-language"
                                        value={createLanguage}
                                        onChange={(e) => setCreateLanguage(e.target.value as "" | Language)}
                                        className={fieldClass}
                                    >
                                        <option value="">My default language</option>
                                        <option value="nl">Nederlands</option>
                                        <option value="fr">Français</option>
                                        <option value="en">English</option>
                                    </select>
                                </div>
                                {createError && (
                                    <p role="alert" className="flex items-center gap-2 text-sm text-error-primary">
                                        <AlertCircle className="size-4 shrink-0" aria-hidden="true" />
                                        {createError}
                                    </p>
                                )}
                                <div>
                                    <Button type="submit" size="md" isLoading={creating} isDisabled={creating || tailoring} showTextWhileLoading>
                                        {creating ? "Writing your CV..." : createError ? "Try again" : "Create my CV"}
                                    </Button>
                                </div>
                            </form>
                        )}
                    </Panel>

                    <Panel id="tailor-panel" title="Tailor my CV to a posting">
                        <form onSubmit={onTailor} className="flex flex-col gap-5 p-5">
                            <fieldset className="flex flex-col gap-3">
                                <legend className="mb-3 text-sm font-medium text-secondary">Your CV</legend>
                                <RadioGroup
                                    aria-label="How to add your CV"
                                    value={cvSource}
                                    onChange={(value) => setCvSource(value as "paste" | "upload")}
                                    className="flex-row flex-wrap gap-x-6 gap-y-2"
                                >
                                    <RadioButton value="paste" label="Paste text" />
                                    <RadioButton value="upload" label="Upload PDF or Word" />
                                </RadioGroup>

                                {cvSource === "upload" && (
                                    <div className="flex flex-col gap-2">
                                        <InputFile
                                            size="md"
                                            label="CV file"
                                            hint="PDF or .docx, up to 5 MB. We read the text and do not keep the file."
                                            acceptedFileTypes={[".pdf", ".docx"]}
                                            buttonText="Choose file"
                                            isLoading={extracting}
                                            isDisabled={extracting}
                                            isInvalid={!!extractError}
                                            onChange={(files) => onExtract(files?.[0] ?? null)}
                                        />
                                        {extracting && <p role="status" className="text-sm text-tertiary">Reading your file...</p>}
                                        {extractError && (
                                            <div role="alert" className="flex flex-wrap items-center gap-3">
                                                <p className="flex items-center gap-2 text-sm text-error-primary">
                                                    <AlertCircle className="size-4 shrink-0" aria-hidden="true" />
                                                    {extractError}
                                                </p>
                                                {cvFile && cvFile.size <= MAX_FILE_BYTES && (
                                                    <Button size="sm" color="secondary" onPress={() => onExtract(cvFile)}>
                                                        Try again
                                                    </Button>
                                                )}
                                            </div>
                                        )}
                                    </div>
                                )}

                                {showCvText && (
                                    <div className="flex flex-col gap-1.5">
                                        <label htmlFor="cv-text" className={cvSource === "upload" ? "text-sm font-medium text-secondary" : "sr-only"}>
                                            {cvSource === "upload" ? "Text from your file" : "CV text"}
                                        </label>
                                        {cvSource === "upload" && (
                                            <p id="cv-text-hint" className="text-sm text-tertiary">
                                                Check this text and fix anything that came out wrong. Only this text is used.
                                            </p>
                                        )}
                                        <textarea
                                            id="cv-text"
                                            required
                                            rows={10}
                                            maxLength={MAX_CHARS}
                                            value={cvText}
                                            onChange={(e) => setCvText(e.target.value)}
                                            placeholder="Paste your CV as plain text"
                                            aria-invalid={!!cvError}
                                            aria-describedby={cx(cvSource === "upload" && "cv-text-hint", cvError && "cv-text-error") || undefined}
                                            className={fieldClass}
                                        />
                                    </div>
                                )}
                                {cvError && <p id="cv-text-error" className="text-sm text-error-primary">{cvError}</p>}
                            </fieldset>

                            <fieldset className="flex flex-col gap-3">
                                <legend className="mb-3 text-sm font-medium text-secondary">Job posting</legend>
                                <RadioGroup
                                    aria-label="Which job posting"
                                    value={postingSource}
                                    onChange={(value) => setPostingSource(value as "saved" | "paste")}
                                    className="flex-row flex-wrap gap-x-6 gap-y-2"
                                >
                                    <RadioButton value="saved" label="One of my postings" />
                                    <RadioButton value="paste" label="Paste text" />
                                </RadioGroup>

                                {postingSource === "saved" &&
                                    (postingsError ? (
                                        <div role="alert" className="flex flex-wrap items-center gap-3">
                                            <p className="text-sm text-error-primary">Could not load your postings.</p>
                                            <Button size="sm" color="secondary" onPress={loadPostings}>
                                                Try again
                                            </Button>
                                        </div>
                                    ) : postings === null ? (
                                        <p role="status" className="text-sm text-tertiary">Loading your postings...</p>
                                    ) : postings.length === 0 ? (
                                        <p className="text-sm text-tertiary">
                                            You have no saved postings yet. Save one on the{" "}
                                            <Link to="/postings" className="font-semibold text-brand-secondary underline-offset-2 hover:underline focus-visible:underline">
                                                Postings
                                            </Link>{" "}
                                            page, or paste the text instead.
                                        </p>
                                    ) : (
                                        <div className="flex flex-col gap-1.5">
                                            <label htmlFor="posting-id" className="sr-only">Saved posting</label>
                                            <select
                                                id="posting-id"
                                                required
                                                value={postingId}
                                                onChange={(e) => setPostingId(e.target.value)}
                                                aria-invalid={!!postingError}
                                                aria-describedby={postingError ? "posting-error" : undefined}
                                                className={fieldClass}
                                            >
                                                <option value="">Choose a posting</option>
                                                {postings.map((p) => (
                                                    <option key={p.id} value={p.id}>
                                                        {p.city ? `${p.title}, ${p.company} (${p.city})` : `${p.title}, ${p.company}`}
                                                    </option>
                                                ))}
                                            </select>
                                        </div>
                                    ))}

                                {postingSource === "paste" && (
                                    <div className="flex flex-col gap-1.5">
                                        <label htmlFor="posting-text" className="sr-only">Job posting text</label>
                                        <textarea
                                            id="posting-text"
                                            required
                                            rows={8}
                                            maxLength={MAX_CHARS}
                                            value={postingText}
                                            onChange={(e) => setPostingText(e.target.value)}
                                            placeholder="Paste the full job posting"
                                            aria-invalid={!!postingError}
                                            aria-describedby={postingError ? "posting-error" : undefined}
                                            className={fieldClass}
                                        />
                                    </div>
                                )}
                                {postingError && <p id="posting-error" className="text-sm text-error-primary">{postingError}</p>}
                            </fieldset>

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
                                <Button type="submit" size="md" isLoading={tailoring} isDisabled={tailoring || extracting || creating} showTextWhileLoading>
                                    {tailoring ? "Tailoring your CV..." : "Tailor CV"}
                                </Button>
                            </div>
                        </form>
                    </Panel>

                <Panel id="draft-panel" title="Cover letter or recruiter message">
                    <form onSubmit={onDraft} className="flex flex-col gap-5 p-5">
                        {postingsError ? (
                            <div className="flex flex-col items-start gap-3">
                                <p className="text-sm text-error-primary">Could not load your postings.</p>
                                <Button size="sm" color="secondary" onPress={loadPostings}>Try again</Button>
                            </div>
                        ) : postings === null ? (
                            <p role="status" className="text-sm text-tertiary">Loading...</p>
                        ) : postings.length === 0 ? (
                            <p className="text-sm text-tertiary">Add a posting first. Drafts are written for one of your postings.</p>
                        ) : (
                            <>
                                <div className="flex flex-col gap-1.5">
                                    <label htmlFor="draft-posting" className="text-sm font-medium text-secondary">Posting</label>
                                    <select id="draft-posting" required value={draftPostingId} onChange={(e) => setDraftPostingId(e.target.value)} className={fieldClass}>
                                        <option value="">Choose a posting</option>
                                        {postings.map((p) => (
                                            <option key={p.id} value={p.id}>{p.company} - {p.title}</option>
                                        ))}
                                    </select>
                                </div>
                                <div className="grid gap-5 sm:grid-cols-2">
                                    <div className="flex flex-col gap-1.5">
                                        <label htmlFor="draft-kind" className="text-sm font-medium text-secondary">Type</label>
                                        <select id="draft-kind" value={draftKind} onChange={(e) => setDraftKind(e.target.value as DraftKind)} className={fieldClass}>
                                            <option value="coverLetter">Cover letter</option>
                                            <option value="recruiterMessage">Recruiter message (max {RECRUITER_MAX} characters)</option>
                                        </select>
                                    </div>
                                    <div className="flex flex-col gap-1.5">
                                        <label htmlFor="draft-language" className="text-sm font-medium text-secondary">Language</label>
                                        <select id="draft-language" value={draftLanguage} onChange={(e) => setDraftLanguage(e.target.value as "" | Language)} className={fieldClass}>
                                            <option value="">Same as posting</option>
                                            <option value="nl">Nederlands</option>
                                            <option value="fr">Français</option>
                                            <option value="en">English</option>
                                        </select>
                                    </div>
                                </div>
                                {draftError && (
                                    <p role="alert" className="flex items-center gap-2 text-sm text-error-primary">
                                        <AlertCircle className="size-4 shrink-0" aria-hidden="true" />
                                        {draftError}
                                    </p>
                                )}
                                <div>
                                    <Button type="submit" size="md" isLoading={drafting} isDisabled={drafting || !draftPostingId} showTextWhileLoading>
                                        {drafting ? "Writing your draft..." : "Write draft"}
                                    </Button>
                                </div>
                            </>
                        )}
                    </form>
                </Panel>
                </div>

                <Panel
                    title={current ? current.title : "Your draft"}
                    action={
                        current && !editing ? (
                            <div className="flex flex-wrap gap-2">
                                <Button size="sm" color="secondary" iconLeading={Copy01} onPress={onCopy}>
                                    {copied ? "Copied" : "Copy"}
                                </Button>
                                <Button size="sm" color="secondary" iconLeading={Edit05} onPress={onEdit}>
                                    Edit
                                </Button>
                                <Button size="sm" color="secondary" iconLeading={Download01} onPress={onDownload} isLoading={downloading} isDisabled={downloading} showTextWhileLoading>
                                    Download Word (.docx)
                                </Button>
                                <Button size="sm" color="secondary" iconLeading={Printer} onPress={() => window.print()}>
                                    Print / Save as PDF
                                </Button>
                            </div>
                        ) : undefined
                    }
                >
                    <div className="flex flex-col gap-4 p-5">
                        {!current && !tailoring && !creating && (
                            <p className="text-sm text-tertiary">Your CV shows up here. Every line in it comes from your profile or your own CV.</p>
                        )}
                        {!current && (tailoring || creating) && (
                            <p role="status" className="text-sm text-tertiary">
                                {creating ? "Writing your CV from your profile." : "Rewriting your CV for this posting."} This takes up to a minute.
                            </p>
                        )}

                        {current?.kind === "recruiterMessage" && !editing && (
                            <p className={cx("text-sm", current.content.length > RECRUITER_MAX ? "text-error-primary" : "text-tertiary")}>
                                {current.content.length}/{RECRUITER_MAX} characters. Copy it and send it yourself.
                            </p>
                        )}

                        {downloadError && !editing && (
                            <div role="alert" className="flex flex-wrap items-center gap-3">
                                <p className="text-sm text-error-primary">{downloadError}</p>
                                <Button size="sm" color="secondary" onPress={onDownload}>
                                    Try again
                                </Button>
                            </div>
                        )}

                        {current && !!current.droppedCount && (
                            <p className="rounded-lg bg-warning-primary px-3 py-2 text-sm text-warning-primary">
                                {current.droppedCount} {current.droppedCount === 1 ? "line was" : "lines were"} removed because they could not be matched to your profile or CV.
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
                                    <textarea
                                        id="edit-content"
                                        rows={current.kind === "recruiterMessage" ? 6 : 18}
                                        maxLength={current.kind === "recruiterMessage" ? RECRUITER_MAX : undefined}
                                        value={draftContent}
                                        onChange={(e) => setDraftContent(e.target.value)}
                                        className={cx(fieldClass, "font-mono text-sm")}
                                    />
                                    {current.kind === "recruiterMessage" && (
                                        <p className="text-sm text-tertiary">{draftContent.length}/{RECRUITER_MAX} characters</p>
                                    )}
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

            <Panel title="Your documents">
                {listError ? (
                    <div className="flex flex-col items-start gap-3 p-5">
                        <p className="text-sm text-error-primary">Could not load your documents.</p>
                        <Button size="sm" color="secondary" onPress={loadList}>
                            Try again
                        </Button>
                    </div>
                ) : list === null ? (
                    <p role="status" className="p-5 text-sm text-tertiary">Loading...</p>
                ) : list.length === 0 ? (
                    <p className="p-5 text-sm text-tertiary">No documents yet. Use one of the forms above to make your first one.</p>
                ) : (
                    <ul className="flex flex-col divide-y divide-secondary">
                        {list.map((doc) => (
                            <li key={doc.id} className="flex flex-wrap items-center gap-3 px-5 py-3">
                                <div className="min-w-0 flex-1">
                                    <p className="truncate text-sm font-medium text-primary">{doc.title}</p>
                                    <p className="text-sm text-tertiary">{formatDate(doc.createdAt)}</p>
                                </div>
                                <Badge size="sm" color="brand">{kindNames[doc.kind] ?? doc.kind}</Badge>
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
