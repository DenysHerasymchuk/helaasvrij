import { type ReactNode, useCallback, useEffect, useState } from "react";
import { FaGithub } from "react-icons/fa";
import { Check, File05, Globe01, Trash01, Type01 } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { Input } from "@/components/base/input/input";
import { InputFile } from "@/components/base/input/input-file";
import { ApiError } from "@/core/api";
import { JourneyBar } from "@/features/journey/journey-bar";
import { BelgianDateInput, Field, NativeSelect, TextArea } from "@/features/postings/fields";
import { formatDateTime } from "@/features/postings/types";
import {
    type ExtractResult,
    type Profile,
    type ProfileItem,
    type ProfileItemKind,
    type Source,
    addSite,
    addText,
    cefrLevels,
    confirmAllItems,
    deleteProfileItem,
    deleteSource,
    extractProfile,
    getProfile,
    listSources,
    saveBasics,
    setItemConfirmed,
    setItemLevel,
    syncGitHub,
    uploadCv,
} from "./api";
import { PageHeader } from "@/core/ui";

const message = (error: unknown) => (error instanceof ApiError ? error.message : "Something went wrong. Try again.");

const kindOrder: { kind: ProfileItemKind; title: string }[] = [
    { kind: "Skill", title: "Skills" },
    { kind: "Project", title: "Projects" },
    { kind: "Experience", title: "Experience" },
    { kind: "Language", title: "Languages" },
    { kind: "Education", title: "Education" },
];

const sourceTitle = { GitHub: "GitHub", Cv: "CV or LinkedIn PDF", Site: "Website", PastedText: "Pasted text" } as const;

function Section({ title, description, action, children }: { title: string; description: string; action?: ReactNode; children: ReactNode }) {
    return (
        <section className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
            <header className="flex flex-wrap items-start justify-between gap-3 border-b border-secondary px-5 py-4">
                <div>
                    <h2 className="text-md font-semibold text-primary">{title}</h2>
                    <p className="mt-0.5 text-sm text-tertiary">{description}</p>
                </div>
                {action}
            </header>
            <div className="px-5 py-5">{children}</div>
        </section>
    );
}

function ErrorBox({ text, onRetry }: { text: string; onRetry: () => void }) {
    return (
        <div role="alert" className="flex flex-col items-start gap-3 rounded-xl bg-primary p-5 shadow-xs ring-1 ring-secondary">
            <p className="text-sm text-error-primary">{text}</p>
            <Button color="secondary" size="sm" onPress={onRetry}>
                Try again
            </Button>
        </div>
    );
}

function formatDate(iso: string | null) {
    return iso ? formatDateTime(iso) : "never";
}

export function ProfilePage() {
    const [sources, setSources] = useState<Source[] | null>(null);
    const [profile, setProfile] = useState<Profile | null>(null);
    const [loadError, setLoadError] = useState<string | null>(null);

    const loadSources = useCallback(async () => setSources((await listSources()) ?? []), []);
    const loadProfile = useCallback(async () => setProfile(await getProfile()), []);

    const load = useCallback(() => {
        setLoadError(null);
        Promise.all([loadSources(), loadProfile()]).catch((error: unknown) => setLoadError(message(error)));
    }, [loadSources, loadProfile]);

    useEffect(load, [load]);

    return (
        <div className="flex max-w-4xl flex-col gap-6 px-4 py-8 md:px-8">
            <PageHeader title="Profile" description="Connect your sources, then confirm what we found. Drafts only use items you confirm." />
            <JourneyBar />

            {loadError ? (
                <ErrorBox text={loadError} onRetry={load} />
            ) : !sources || !profile ? (
                <div aria-busy="true" aria-label="Loading profile" className="flex flex-col gap-6">
                    {[0, 1, 2].map((key) => (
                        <div key={key} className="h-40 animate-pulse rounded-xl bg-tertiary" />
                    ))}
                </div>
            ) : (
                <>
                    <SourcesSection sources={sources} reload={async () => { await Promise.all([loadSources(), loadProfile()]); }} />
                    <ItemsSection sources={sources} profile={profile} reloadProfile={loadProfile} />
                    <BasicsSection key={JSON.stringify(profile.basics)} basics={profile.basics} onSaved={(basics) => setProfile({ ...profile, basics })} />
                </>
            )}
        </div>
    );
}

// ---------- Sources ----------

function SourcesSection({ sources, reload }: { sources: Source[]; reload: () => Promise<void> }) {
    const [busy, setBusy] = useState<string | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [username, setUsername] = useState("");
    const [url, setUrl] = useState("");
    const [pasting, setPasting] = useState(false);
    const [text, setText] = useState("");

    const run = async (key: string, action: () => Promise<unknown>, done?: () => void) => {
        setBusy(key);
        setError(null);
        try {
            await action();
            done?.();
            await reload();
        } catch (caught) {
            setError(message(caught));
        } finally {
            setBusy(null);
        }
    };

    return (
        <Section title="Sources" description="Where your profile comes from. Only your own links and files; LinkedIn data comes from your own PDF export.">
            {sources.length === 0 ? (
                <p className="mb-5 text-sm text-tertiary">No sources yet. Add at least one below.</p>
            ) : (
                <ul className="mb-5 flex flex-col divide-y divide-secondary rounded-lg ring-1 ring-secondary">
                    {sources.map((source) => (
                        <li key={source.id} className="flex flex-wrap items-center gap-3 px-4 py-3">
                            <SourceIcon kind={source.kind} />
                            <div className="min-w-0 flex-1 basis-52">
                                <p className="truncate text-sm font-medium text-primary">
                                    {sourceTitle[source.kind]}: {source.label}
                                </p>
                                <p className="text-sm text-tertiary">
                                    Last sync {formatDate(source.lastSyncedAt)}
                                    {source.status === "Ready" && `, ${source.documentCount} document${source.documentCount === 1 ? "" : "s"}`}
                                </p>
                                {source.error && <p className="text-sm text-error-primary">{source.error}</p>}
                            </div>
                            <Badge type="pill-color" size="sm" color={source.status === "Ready" ? "success" : source.status === "Failed" ? "error" : "gray"}>
                                {source.status === "Ready" ? "Ready" : source.status === "Failed" ? "Failed" : "Pending"}
                            </Badge>
                            <Button
                                color="tertiary-destructive"
                                size="sm"
                                iconLeading={Trash01}
                                aria-label={`Remove ${source.label}`}
                                isLoading={busy === `rm-${source.id}`}
                                onPress={() => run(`rm-${source.id}`, () => deleteSource(source.id))}
                            >
                                Remove
                            </Button>
                        </li>
                    ))}
                </ul>
            )}

            <div className="grid gap-4 md:grid-cols-2">
                <form
                    className="flex flex-col gap-2"
                    onSubmit={(event) => {
                        event.preventDefault();
                        run("github", () => syncGitHub(username.trim() || undefined), () => setUsername(""));
                    }}
                >
                    <Input label="GitHub username" placeholder="octocat" value={username} onChange={setUsername} hint="Public repositories only. Leave empty to use your signed-in GitHub account." />
                    <Button type="submit" color="secondary" size="md" iconLeading={FaGithub} isLoading={busy === "github"} className="self-start">
                        Sync GitHub
                    </Button>
                </form>

                <form
                    className="flex flex-col gap-2"
                    onSubmit={(event) => {
                        event.preventDefault();
                        run("site", () => addSite(url.trim()), () => setUrl(""));
                    }}
                >
                    <Input label="Personal website" placeholder="https://example.com" value={url} onChange={setUrl} hint="One public page." />
                    <Button type="submit" color="secondary" size="md" iconLeading={Globe01} isLoading={busy === "site"} isDisabled={!url.trim()} className="self-start">
                        Add website
                    </Button>
                </form>

                <InputFile
                    size="md"
                    label="CV or LinkedIn PDF"
                    placeholder="Choose a PDF"
                    buttonText="Upload PDF"
                    acceptedFileTypes={["application/pdf", ".pdf"]}
                    isLoading={busy === "cv"}
                    isDisabled={busy === "cv"}
                    hint="PDF up to 5 MB. LinkedIn: Save to PDF from your profile."
                    onChange={(files) => {
                        const file = files?.[0];
                        if (file) run("cv", () => uploadCv(file));
                    }}
                />

                <div className="flex flex-col items-start gap-2">
                    <span className="text-sm font-medium text-secondary">No PDF?</span>
                    <Button color="secondary" size="md" iconLeading={Type01} onPress={() => setPasting((open) => !open)}>
                        Paste text instead
                    </Button>
                </div>
            </div>

            {pasting && (
                <form
                    className="mt-4 flex flex-col gap-3"
                    onSubmit={(event) => {
                        event.preventDefault();
                        run("text", () => addText("Pasted text", text), () => {
                            setText("");
                            setPasting(false);
                        });
                    }}
                >
                    <Field label="Your CV or profile text" hint="Up to 20,000 characters.">
                        <TextArea rows={8} value={text} onChange={(event) => setText(event.target.value)} maxLength={20000} />
                    </Field>
                    <Button type="submit" size="md" isLoading={busy === "text"} isDisabled={!text.trim()} className="self-start">
                        Add text
                    </Button>
                </form>
            )}

            {error && (
                <p role="alert" className="mt-4 text-sm text-error-primary">
                    {error}
                </p>
            )}
        </Section>
    );
}

function SourceIcon({ kind }: { kind: Source["kind"] }) {
    const Icon = kind === "GitHub" ? FaGithub : kind === "Site" ? Globe01 : kind === "Cv" ? File05 : Type01;
    return (
        <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-secondary text-fg-quaternary">
            <Icon className="size-5" />
        </span>
    );
}

// ---------- Extracted items ----------

function ItemsSection({ sources, profile, reloadProfile }: { sources: Source[]; profile: Profile; reloadProfile: () => Promise<void> }) {
    const [extracting, setExtracting] = useState(false);
    const [result, setResult] = useState<ExtractResult | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [confirmingAll, setConfirmingAll] = useState(false);

    const hasReady = sources.some((source) => source.status === "Ready");
    const pending = profile.items.filter((item) => !item.confirmed).length;

    const build = async () => {
        setExtracting(true);
        setError(null);
        setResult(null);
        try {
            setResult((await extractProfile()) ?? null);
            await reloadProfile();
        } catch (caught) {
            setError(message(caught));
        } finally {
            setExtracting(false);
        }
    };

    const confirmAll = async () => {
        setConfirmingAll(true);
        setError(null);
        try {
            await confirmAllItems();
            await reloadProfile();
        } catch (caught) {
            setError(message(caught));
        } finally {
            setConfirmingAll(false);
        }
    };

    return (
        <Section
            title="What we found"
            description="Every item comes with a quote from your own sources. Confirm what is true, remove what is not."
            action={
                <div className="flex flex-wrap gap-2">
                    {pending > 0 && (
                        <Button color="secondary" size="md" iconLeading={Check} isLoading={confirmingAll} onPress={confirmAll}>
                            Confirm all ({pending})
                        </Button>
                    )}
                    <Button size="md" isLoading={extracting} isDisabled={!hasReady} onPress={build}>
                        {extracting ? "Reading your sources..." : "Build my profile"}
                    </Button>
                </div>
            }
        >
            <div role="status" className="flex flex-col gap-1 empty:hidden">
                {extracting && <p className="text-sm text-tertiary">This can take up to a minute.</p>}
                {result && (
                    <p className="text-sm text-secondary">
                        Added {result.created} new item{result.created === 1 ? "" : "s"}, merged {result.merged} quote{result.merged === 1 ? "" : "s"} into
                        existing items.
                        {result.skippedDocuments > 0 && ` ${result.skippedDocuments} document${result.skippedDocuments === 1 ? " was" : "s were"} skipped to stay within limits; run it again.`}
                    </p>
                )}
                {result && result.dropped > 0 && (
                    <p className="text-sm text-warning-primary">
                        {result.dropped} claim{result.dropped === 1 ? "" : "s"} dropped because the quote was not found in the source.
                    </p>
                )}
            </div>
            {error && (
                <p role="alert" className="text-sm text-error-primary">
                    {error}
                </p>
            )}

            {profile.items.length === 0 ? (
                <p className="text-sm text-tertiary">
                    {hasReady ? "Nothing here yet. Select Build my profile to read your sources." : "Add a source first, then build your profile."}
                </p>
            ) : (
                <div className="mt-2 flex flex-col gap-6">
                    {kindOrder.map(({ kind, title }) => {
                        const items = profile.items.filter((item) => item.kind === kind);
                        if (items.length === 0) return null;
                        return (
                            <div key={kind}>
                                <h3 className="mb-2 text-sm font-semibold text-primary">
                                    {title} <span className="font-normal text-tertiary">({items.length})</span>
                                </h3>
                                <ul className="flex flex-col gap-3">
                                    {items.map((item) => (
                                        <ItemCard key={item.id} item={item} reload={reloadProfile} />
                                    ))}
                                </ul>
                            </div>
                        );
                    })}
                </div>
            )}
        </Section>
    );
}

function ItemCard({ item, reload }: { item: ProfileItem; reload: () => Promise<void> }) {
    const [busy, setBusy] = useState<"confirm" | "remove" | "level" | null>(null);
    const [error, setError] = useState<string | null>(null);

    const run = async (kind: "confirm" | "remove" | "level", action: () => Promise<unknown>) => {
        setBusy(kind);
        setError(null);
        try {
            await action();
            await reload();
        } catch (caught) {
            setError(message(caught));
        } finally {
            setBusy(null);
        }
    };

    return (
        <li className="flex flex-col gap-3 rounded-lg p-4 ring-1 ring-secondary">
            <div className="flex flex-wrap items-center gap-2">
                <span className="text-md font-semibold text-primary">{item.name}</span>
                {item.level && item.kind !== "Language" && (
                    <Badge type="pill-color" size="sm" color="brand">
                        {item.level}
                    </Badge>
                )}
                {item.kind === "Language" && (
                    <NativeSelect
                        aria-label={`CEFR level for ${item.name}`}
                        value={item.level ?? ""}
                        disabled={busy === "level"}
                        onChange={(event) => run("level", () => setItemLevel(item.id, event.target.value || null))}
                        className="w-auto py-1 text-sm"
                    >
                        <option value="">No level</option>
                        {cefrLevels.map((level) => (
                            <option key={level} value={level}>
                                {level}
                            </option>
                        ))}
                    </NativeSelect>
                )}
                {item.confirmed && (
                    <Badge type="pill-color" size="sm" color="success">
                        Confirmed
                    </Badge>
                )}
                <div className="ml-auto flex gap-2">
                    <Button
                        size="sm"
                        color={item.confirmed ? "secondary" : "primary"}
                        isLoading={busy === "confirm"}
                        onPress={() => run("confirm", () => setItemConfirmed(item.id, !item.confirmed))}
                    >
                        {item.confirmed ? "Unconfirm" : "Confirm"}
                    </Button>
                    <Button
                        size="sm"
                        color="tertiary-destructive"
                        aria-label={`Remove ${item.name}`}
                        isLoading={busy === "remove"}
                        onPress={() => run("remove", () => deleteProfileItem(item.id))}
                    >
                        Remove
                    </Button>
                </div>
            </div>

            <ul className="flex flex-col gap-2">
                {item.evidence.map((evidence, index) => (
                    <li key={index}>
                        <blockquote className="rounded-md bg-secondary px-3 py-2 text-sm whitespace-pre-line text-tertiary">{evidence.quote}</blockquote>
                        <p className="mt-1 text-xs text-quaternary">
                            {evidence.url ? (
                                <a href={evidence.url} target="_blank" rel="noreferrer" className="underline underline-offset-2 hover:text-secondary">
                                    {evidence.documentPath}
                                </a>
                            ) : /^https?:\/\//.test(evidence.documentPath) ? (
                                <a href={evidence.documentPath} target="_blank" rel="noreferrer" className="underline underline-offset-2 hover:text-secondary">
                                    {evidence.documentPath}
                                </a>
                            ) : (
                                <span>{evidence.documentPath}</span>
                            )}
                            {" · "}
                            {sourceTitle[evidence.sourceKind]}
                        </p>
                    </li>
                ))}
            </ul>
            {error && (
                <p role="alert" className="text-sm text-error-primary">
                    {error}
                </p>
            )}
        </li>
    );
}

// ---------- Basics ----------

function BasicsSection({ basics, onSaved }: { basics: Profile["basics"]; onSaved: (basics: Profile["basics"]) => void }) {
    const [school, setSchool] = useState(basics.school ?? "");
    const [start, setStart] = useState(basics.stageStart ?? "");
    const [end, setEnd] = useState(basics.stageEnd ?? "");
    const [state, setState] = useState<"idle" | "saving" | "saved">("idle");
    const [error, setError] = useState<ApiError | string | null>(null);

    const save = async () => {
        setState("saving");
        setError(null);
        try {
            const saved = await saveBasics({ school: school.trim() || null, stageStart: start || null, stageEnd: end || null });
            if (saved) onSaved(saved);
            setState("saved");
        } catch (caught) {
            setError(caught instanceof ApiError ? caught : message(caught));
            setState("idle");
        }
    };

    const fieldError = (name: string) => (error instanceof ApiError ? error.fieldErrors[name] : undefined);
    const generalError = typeof error === "string" ? error : error instanceof ApiError && Object.keys(error.fieldErrors).length === 0 ? error.message : null;
    const edit = (set: (value: string) => void) => (value: string) => {
        set(value);
        setState("idle");
    };

    return (
        <Section title="School and stage period" description="Postings with internship dates outside this period are flagged on the postings list.">
            <div className="grid gap-4 md:grid-cols-3">
                <div className="md:col-span-3">
                    <Input label="School" placeholder="Howest, Thomas More, KU Leuven..." value={school} onChange={edit(setSchool)} isInvalid={!!fieldError("school")} hint={fieldError("school")} />
                </div>
                <BelgianDateInput label="Stage starts" value={start} max={end || undefined} onChange={edit(setStart)} />
                <BelgianDateInput label="Stage ends" value={end} min={start || undefined} onChange={edit(setEnd)} isInvalid={!!fieldError("stageEnd")} hint={fieldError("stageEnd")} />
            </div>
            <div className="mt-5 flex flex-wrap items-center gap-3">
                <Button size="md" isLoading={state === "saving"} onPress={save}>
                    Save
                </Button>
                <span role="status" className="text-sm text-tertiary">
                    {state === "saved" ? "Saved." : ""}
                </span>
                {generalError && (
                    <span role="alert" className="text-sm text-error-primary">
                        {generalError}
                    </span>
                )}
            </div>
        </Section>
    );
}
