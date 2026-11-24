import { type ReactNode, useCallback, useEffect, useState } from "react";
import { FileTrigger } from "react-aria-components";
import { useLocation } from "react-router";
import { FaGithub } from "react-icons/fa";
import { Check, File05, Globe01, RefreshCw01, Trash01, Type01 } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { Input } from "@/components/base/input/input";
import { InputFile } from "@/components/base/input/input-file";
import { ApiError } from "@/core/api";
import { useAuth } from "@/core/auth";
import { JourneyBar } from "@/features/journey/journey-bar";
import { BelgianDateInput, Field, NativeSelect, TextArea } from "@/features/postings/fields";
import { formatDateTime } from "@/features/postings/types";
import { cx } from "@/utils/cx";
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
const plural = (count: number, word: string) => `${count} ${word}${count === 1 ? "" : "s"}`;

const kindOrder: { kind: ProfileItemKind; title: string }[] = [
    { kind: "Skill", title: "Skills" },
    { kind: "Project", title: "Projects" },
    { kind: "Experience", title: "Experience" },
    { kind: "Language", title: "Languages" },
    { kind: "Education", title: "Education" },
];

const sourceTitle = { GitHub: "GitHub", Cv: "CV or LinkedIn PDF", Site: "Website", PastedText: "Pasted text" } as const;

/** Runs one action at a time with a busy key and the server's error message. */
function useRunner(after: () => Promise<void>) {
    const [busy, setBusy] = useState<string | null>(null);
    const [error, setError] = useState<string | null>(null);
    const run = async (key: string, action: () => Promise<unknown>, done?: () => void) => {
        setBusy(key);
        setError(null);
        try {
            await action();
            done?.();
            await after();
        } catch (caught) {
            setError(message(caught));
        } finally {
            setBusy(null);
        }
    };
    return { busy, error, run };
}

function Step({ number, title, description, done, action, children }: {
    number: number;
    title: string;
    description: string;
    done: boolean;
    action?: ReactNode;
    children: ReactNode;
}) {
    return (
        <section aria-labelledby={`step-${number}`} className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
            <header className="flex flex-wrap items-start gap-x-4 gap-y-3 border-b border-secondary px-4 py-4 md:px-5">
                <span
                    aria-hidden="true"
                    className={cx(
                        "flex size-8 shrink-0 items-center justify-center rounded-full text-sm font-semibold",
                        done ? "bg-success-solid text-white" : "bg-secondary text-secondary ring-1 ring-primary",
                    )}
                >
                    {done ? <Check className="size-4" /> : number}
                </span>
                <div className="min-w-0 flex-1 basis-60">
                    <h2 id={`step-${number}`} className="text-md font-semibold text-primary">
                        <span className="sr-only">Step {number}: </span>
                        {title}
                        {done && <span className="sr-only"> (done)</span>}
                    </h2>
                    <p className="mt-0.5 text-sm text-tertiary">{description}</p>
                </div>
                {action}
            </header>
            <div className="px-4 py-5 md:px-5">{children}</div>
        </section>
    );
}

function ErrorText({ text }: { text: string | null }) {
    return text ? (
        <p role="alert" className="text-sm text-error-primary">
            {text}
        </p>
    ) : null;
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
    const reloadAll = useCallback(async () => {
        await Promise.all([loadSources(), loadProfile()]);
    }, [loadSources, loadProfile]);

    const load = useCallback(() => {
        setLoadError(null);
        reloadAll().catch((error: unknown) => setLoadError(message(error)));
    }, [reloadAll]);

    useEffect(load, [load]);

    // Links like /profile#step-3 land on that step once it has rendered.
    const { hash } = useLocation();
    useEffect(() => {
        if (profile && hash) document.getElementById(hash.slice(1))?.scrollIntoView({ block: "start" });
    }, [profile, hash]);

    return (
        <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 py-8 md:px-8">
            <PageHeader title="Profile" description="Four steps: add your sources, let us read them, confirm what is true, add your details." />
            <JourneyBar />

            {loadError ? (
                <div role="alert" className="flex flex-col items-start gap-3 rounded-xl bg-primary p-5 shadow-xs ring-1 ring-secondary">
                    <p className="text-sm text-error-primary">{loadError}</p>
                    <Button color="secondary" size="sm" onPress={load}>
                        Try again
                    </Button>
                </div>
            ) : !sources || !profile ? (
                <div aria-busy="true" aria-label="Loading profile" className="flex flex-col gap-6">
                    {[0, 1, 2].map((key) => (
                        <div key={key} className="h-40 animate-pulse rounded-xl bg-tertiary" />
                    ))}
                </div>
            ) : (
                <>
                    <SourcesStep sources={sources} reload={reloadAll} />
                    <ReadStep sources={sources} profile={profile} reloadProfile={loadProfile} />
                    <ItemsStep profile={profile} reloadProfile={loadProfile} />
                    <DetailsStep key={JSON.stringify(profile.basics)} profile={profile} reloadProfile={loadProfile} onSaved={(basics) => setProfile({ ...profile, basics })} />
                </>
            )}
        </div>
    );
}

// ---------- Step 1: sources ----------

function SourcesStep({ sources, reload }: { sources: Source[]; reload: () => Promise<void> }) {
    return (
        <Step
            number={1}
            title="Add your sources"
            description="Add at least one. We only read what you add here; LinkedIn data comes from your own PDF export."
            done={sources.some((source) => source.status === "Ready")}
        >
            <div className="flex flex-col gap-4">
                <GitHubCard source={sources.find((source) => source.kind === "GitHub")} reload={reload} />
                <PdfCard sources={sources.filter((source) => source.kind === "Cv" || source.kind === "PastedText")} reload={reload} />
                <SiteCard sources={sources.filter((source) => source.kind === "Site")} reload={reload} />
            </div>
        </Step>
    );
}

function SourceCard({ icon: Icon, title, status, children }: { icon: typeof Globe01; title: string; status: Source["status"] | null; children: ReactNode }) {
    return (
        <div className="rounded-lg p-4 ring-1 ring-secondary">
            <div className="mb-3 flex items-center gap-3">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-secondary text-fg-secondary">
                    <Icon className="size-5" />
                </span>
                <h3 className="flex-1 text-sm font-semibold text-primary">{title}</h3>
                <Badge type="pill-color" size="sm" color={status === "Ready" ? "success" : status === "Failed" ? "error" : "gray"}>
                    {status === "Ready" ? "Added" : status === "Failed" ? "Failed" : status === "Pending" ? "Reading" : "Not added"}
                </Badge>
            </div>
            <div className="flex flex-col gap-3">{children}</div>
        </div>
    );
}

/** One added source: what it is, what we got from it, and its buttons. */
function SourceRow({ name, detail, error, actions }: { name: ReactNode; detail: string; error: string | null; actions: ReactNode }) {
    return (
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2 rounded-md bg-secondary px-3 py-2.5">
            <div className="min-w-0 flex-1 basis-48">
                <p className="truncate text-sm font-medium text-primary">{name}</p>
                <p className="text-sm text-tertiary">{detail}</p>
                {error && <p className="text-sm text-error-primary">{error}</p>}
            </div>
            <div className="flex flex-wrap gap-1">{actions}</div>
        </div>
    );
}

function RemoveButton({ label, busy, onPress }: { label: string; busy: boolean; onPress: () => void }) {
    return (
        <Button color="tertiary-destructive" size="sm" iconLeading={Trash01} aria-label={`Remove ${label}`} isLoading={busy} onPress={onPress}>
            Remove
        </Button>
    );
}

function GitHubCard({ source, reload }: { source: Source | undefined; reload: () => Promise<void> }) {
    const { user } = useAuth();
    const [username, setUsername] = useState(user?.gitHubLogin ?? "");
    const { busy, error, run } = useRunner(reload);
    // shortcut: an API older than repoCount sends none, fall back to documents (two per repo).
    const repos = source ? (source.repoCount ?? Math.ceil(source.documentCount / 2)) : 0;

    return (
        <SourceCard icon={FaGithub} title="GitHub" status={source?.status ?? null}>
            {source ? (
                <SourceRow
                    name={
                        <a href={`https://github.com/${source.label}`} target="_blank" rel="noreferrer" className="rounded-xs underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-focus-ring">
                            github.com/{source.label}
                        </a>
                    }
                    detail={
                        source.status === "Ready"
                            ? `${repos} ${repos === 1 ? "repository" : "repositories"}, last synced ${formatDate(source.lastSyncedAt)}`
                            : `Last tried ${formatDate(source.lastSyncedAt)}`
                    }
                    error={source.error}
                    actions={
                        <>
                            <Button color="secondary" size="sm" iconLeading={RefreshCw01} isLoading={busy === "sync"} onPress={() => run("sync", () => syncGitHub(source.label))}>
                                Sync again
                            </Button>
                            <RemoveButton label={`GitHub ${source.label}`} busy={busy === "remove"} onPress={() => run("remove", () => deleteSource(source.id))} />
                        </>
                    }
                />
            ) : (
                <form
                    className="flex flex-col gap-2 sm:flex-row sm:items-start"
                    onSubmit={(event) => {
                        event.preventDefault();
                        run("add", () => syncGitHub(username.trim()));
                    }}
                >
                    <Input
                        className="flex-1"
                        label="GitHub username"
                        placeholder="octocat"
                        value={username}
                        onChange={setUsername}
                        hint="We read up to 10 of your public repositories (no forks)."
                    />
                    <Button type="submit" size="md" isLoading={busy === "add"} isDisabled={!username.trim()} className="self-start sm:mt-6">
                        Add
                    </Button>
                </form>
            )}
            {busy === "add" || busy === "sync" ? (
                <p role="status" className="text-sm text-tertiary">
                    Reading your repositories. This can take a few seconds.
                </p>
            ) : null}
            <ErrorText text={error} />
        </SourceCard>
    );
}

function PdfCard({ sources, reload }: { sources: Source[]; reload: () => Promise<void> }) {
    const { busy, error, run } = useRunner(reload);
    const [pasting, setPasting] = useState(false);
    const [text, setText] = useState("");
    const status = sources.some((source) => source.status === "Ready") ? "Ready" : sources[0]?.status ?? null;

    return (
        <SourceCard icon={File05} title="CV or LinkedIn PDF" status={status}>
            {sources.map((source) => (
                <SourceRow
                    key={source.id}
                    name={source.label}
                    detail={
                        source.kind === "Cv"
                            ? `Text read from ${plural(source.documentCount, "page")}, uploaded ${formatDate(source.lastSyncedAt)}`
                            : `Pasted text, added ${formatDate(source.lastSyncedAt)}`
                    }
                    error={source.error}
                    actions={
                        <>
                            {source.kind === "Cv" && (
                                <FileTrigger
                                    acceptedFileTypes={["application/pdf", ".pdf"]}
                                    onSelect={(files) => {
                                        const file = files?.[0];
                                        // Upload first, so a rejected file keeps the old one.
                                        if (file) run(`replace-${source.id}`, async () => {
                                            await uploadCv(file);
                                            await deleteSource(source.id);
                                        });
                                    }}
                                >
                                    <Button color="secondary" size="sm" aria-label={`Replace ${source.label}`} isLoading={busy === `replace-${source.id}`}>
                                        Replace
                                    </Button>
                                </FileTrigger>
                            )}
                            <RemoveButton label={source.label} busy={busy === `rm-${source.id}`} onPress={() => run(`rm-${source.id}`, () => deleteSource(source.id))} />
                        </>
                    }
                />
            ))}

            <InputFile
                size="md"
                label={sources.length ? "Add another PDF" : "Upload your CV or LinkedIn PDF"}
                placeholder="Choose a PDF"
                buttonText="Upload PDF"
                acceptedFileTypes={["application/pdf", ".pdf"]}
                isLoading={busy === "cv"}
                isDisabled={busy === "cv"}
                hint="PDF up to 5 MB. From LinkedIn: open your profile, More, Save to PDF."
                onChange={(files) => {
                    const file = files?.[0];
                    if (file) run("cv", () => uploadCv(file));
                }}
            />

            {pasting ? (
                <form
                    className="flex flex-col gap-3"
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
                    <div className="flex flex-wrap gap-2">
                        <Button type="submit" size="md" isLoading={busy === "text"} isDisabled={!text.trim()}>
                            Add text
                        </Button>
                        <Button color="secondary" size="md" onPress={() => setPasting(false)}>
                            Cancel
                        </Button>
                    </div>
                </form>
            ) : (
                <Button color="link-gray" size="sm" iconLeading={Type01} className="self-start" onPress={() => setPasting(true)}>
                    No PDF? Paste the text instead
                </Button>
            )}
            <ErrorText text={error} />
        </SourceCard>
    );
}

function SiteCard({ sources, reload }: { sources: Source[]; reload: () => Promise<void> }) {
    const { busy, error, run } = useRunner(reload);
    const [url, setUrl] = useState("");
    const [changing, setChanging] = useState(false);
    const current = sources[0];
    const showForm = !current || changing;

    return (
        <SourceCard icon={Globe01} title="Website" status={current?.status ?? null}>
            {sources.map((source) => (
                <SourceRow
                    key={source.id}
                    name={source.label}
                    detail={source.status === "Ready" ? `Page read, ${formatDate(source.lastSyncedAt)}` : `Last tried ${formatDate(source.lastSyncedAt)}`}
                    error={source.error && `${source.error} Check the address and add it again.`}
                    actions={
                        <>
                            {!changing && (
                                <Button color="secondary" size="sm" onPress={() => setChanging(true)}>
                                    Change
                                </Button>
                            )}
                            <RemoveButton label={`website ${source.label}`} busy={busy === `rm-${source.id}`} onPress={() => run(`rm-${source.id}`, () => deleteSource(source.id))} />
                        </>
                    }
                />
            ))}
            {showForm && (
                <form
                    className="flex flex-col gap-2 sm:flex-row sm:items-start"
                    onSubmit={(event) => {
                        event.preventDefault();
                        run(
                            "site",
                            async () => {
                                await addSite(url.trim());
                                // The new page is in; drop the one it replaces.
                                if (changing) await Promise.all(sources.map((source) => deleteSource(source.id)));
                            },
                            () => {
                                setUrl("");
                                setChanging(false);
                            },
                        );
                    }}
                >
                    <Input className="flex-1" type="url" label="Your site" placeholder="https://yourname.be" value={url} onChange={setUrl} hint="A portfolio or personal page. We read that one public page." />
                    <div className="flex gap-2 self-start sm:mt-6">
                        <Button type="submit" size="md" isLoading={busy === "site"} isDisabled={!url.trim()}>
                            {changing ? "Save" : "Add"}
                        </Button>
                        {changing && (
                            <Button color="secondary" size="md" onPress={() => setChanging(false)}>
                                Cancel
                            </Button>
                        )}
                    </div>
                </form>
            )}
            <ErrorText text={error} />
        </SourceCard>
    );
}

// ---------- Step 2: read the sources ----------

function ReadStep({ sources, profile, reloadProfile }: { sources: Source[]; profile: Profile; reloadProfile: () => Promise<void> }) {
    const [reading, setReading] = useState(false);
    const [result, setResult] = useState<ExtractResult | null>(null);
    const [error, setError] = useState<string | null>(null);

    const ready = sources.filter((source) => source.status === "Ready").length;
    const blocked = sources.length === 0 ? "Add GitHub, a PDF or your site in step 1 first." : ready === 0 ? "None of your sources is ready. Fix or remove the failed ones in step 1." : null;

    const read = async () => {
        setReading(true);
        setError(null);
        setResult(null);
        try {
            setResult((await extractProfile()) ?? null);
            await reloadProfile();
        } catch (caught) {
            setError(message(caught));
        } finally {
            setReading(false);
        }
    };

    return (
        <Step
            number={2}
            title="Read my sources"
            description="We read your GitHub, CV and site and list the skills and projects we can prove. You confirm what is right."
            done={profile.items.length > 0}
        >
            <div className="flex flex-col items-start gap-3">
                <Button size="md" isLoading={reading} isDisabled={!!blocked || reading} onPress={read}>
                    {profile.items.length > 0 ? "Read my sources again" : "Read my sources"}
                </Button>
                <div role="status" className="flex flex-col gap-1 text-sm empty:hidden">
                    {blocked && <p className="text-tertiary">{blocked}</p>}
                    {reading && <p className="text-secondary">Reading {plural(ready, "source")}. This takes up to a minute.</p>}
                    {result && (
                        <p className="text-secondary">
                            {result.created > 0
                                ? `Found ${plural(result.created, "new item")}. Confirm the ones that are right in step 3.`
                                : "No new items found. Everything we can prove is already in step 3."}
                            {result.skippedDocuments > 0 && ` We skipped ${plural(result.skippedDocuments, "document")} to stay within limits; read again to include them.`}
                        </p>
                    )}
                    {result && result.dropped > 0 && (
                        <p className="text-warning-primary">We left out {plural(result.dropped, "claim")} because the quote was not in your source.</p>
                    )}
                </div>
                <ErrorText text={error} />
            </div>
        </Step>
    );
}

// ---------- Step 3: confirm items ----------

function ItemsStep({ profile, reloadProfile }: { profile: Profile; reloadProfile: () => Promise<void> }) {
    const { busy, error, run } = useRunner(reloadProfile);
    const pending = profile.items.filter((item) => !item.confirmed).length;

    return (
        <Step
            number={3}
            title="Confirm what is right"
            description="Each item quotes your own source. Your CV, letters and gap plan only use items you confirm."
            done={profile.items.some((item) => item.confirmed)}
            action={
                pending > 0 && (
                    <Button color="secondary" size="md" iconLeading={Check} isLoading={busy === "all"} onPress={() => run("all", confirmAllItems)}>
                        Confirm all ({pending})
                    </Button>
                )
            }
        >
            <ErrorText text={error} />
            {profile.items.length === 0 ? (
                <p className="text-sm text-tertiary">Nothing to confirm yet. Read your sources in step 2.</p>
            ) : (
                <div className="flex flex-col gap-6">
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
        </Step>
    );
}

function ItemCard({ item, reload }: { item: ProfileItem; reload: () => Promise<void> }) {
    const { busy, error, run } = useRunner(reload);

    return (
        <li className="flex flex-col gap-3 rounded-lg p-4 ring-1 ring-secondary">
            <div className="flex flex-wrap items-center gap-2">
                <span className="text-md font-semibold text-primary">{item.name}</span>
                {item.level && (
                    <Badge type="pill-color" size="sm" color="brand">
                        {item.level}
                    </Badge>
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
                        aria-label={`${item.confirmed ? "Unconfirm" : "Confirm"} ${item.name}`}
                        isLoading={busy === "confirm"}
                        onPress={() => run("confirm", () => setItemConfirmed(item.id, !item.confirmed))}
                    >
                        {item.confirmed ? "Unconfirm" : "Confirm"}
                    </Button>
                    <Button size="sm" color="tertiary-destructive" aria-label={`Remove ${item.name}`} isLoading={busy === "remove"} onPress={() => run("remove", () => deleteProfileItem(item.id))}>
                        Remove
                    </Button>
                </div>
            </div>

            <ul className="flex flex-col gap-2">
                {item.evidence.map((evidence, index) => {
                    const href = evidence.url ?? (/^https?:\/\//.test(evidence.documentPath) ? evidence.documentPath : null);
                    return (
                        <li key={index}>
                            <blockquote className="rounded-md bg-secondary px-3 py-2 text-sm whitespace-pre-line text-tertiary">{evidence.quote}</blockquote>
                            <p className="mt-1 text-xs text-quaternary">
                                {sourceTitle[evidence.sourceKind]}:{" "}
                                {href ? (
                                    <a href={href} target="_blank" rel="noreferrer" className="underline underline-offset-2 hover:text-secondary">
                                        {evidence.documentPath}
                                    </a>
                                ) : (
                                    <span>{evidence.documentPath}</span>
                                )}
                            </p>
                        </li>
                    );
                })}
            </ul>
            <ErrorText text={error} />
        </li>
    );
}

// ---------- Step 4: details ----------

function DetailsStep({ profile, reloadProfile, onSaved }: { profile: Profile; reloadProfile: () => Promise<void>; onSaved: (basics: Profile["basics"]) => void }) {
    const { basics } = profile;
    const [school, setSchool] = useState(basics.school ?? "");
    const [start, setStart] = useState(basics.stageStart ?? "");
    const [end, setEnd] = useState(basics.stageEnd ?? "");
    const [state, setState] = useState<"idle" | "saving" | "saved">("idle");
    const [error, setError] = useState<ApiError | string | null>(null);
    const levels = useRunner(reloadProfile);
    const languages = profile.items.filter((item) => item.kind === "Language");

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
        <Step
            number={4}
            title="Your details"
            description="Your school and stage period flag postings with dates that do not fit. Language levels go into your gap plan and CV."
            done={!!(basics.school && basics.stageStart && basics.stageEnd)}
        >
            <div className="grid gap-4 md:grid-cols-2">
                <div className="md:col-span-2">
                    <Input label="School" placeholder="Howest, Thomas More, KU Leuven..." value={school} onChange={edit(setSchool)} isInvalid={!!fieldError("school")} hint={fieldError("school")} />
                </div>
                <BelgianDateInput label="Stage starts" value={start} max={end || undefined} onChange={edit(setStart)} />
                <BelgianDateInput label="Stage ends" value={end} min={start || undefined} onChange={edit(setEnd)} isInvalid={!!fieldError("stageEnd")} hint={fieldError("stageEnd")} />
            </div>
            <div className="mt-5 flex flex-wrap items-center gap-3">
                <Button size="md" isLoading={state === "saving"} onPress={save}>
                    Save details
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

            <div className="mt-6 border-t border-secondary pt-5">
                <h3 className="text-sm font-semibold text-primary">Language levels</h3>
                <p className="mt-0.5 text-sm text-tertiary">Pick the CEFR level you would defend in an interview. Changes save right away.</p>
                {languages.length === 0 ? (
                    <p className="mt-3 text-sm text-tertiary">No languages yet. They show up here after step 2 finds them in your sources.</p>
                ) : (
                    <ul className="mt-3 flex flex-col divide-y divide-secondary rounded-lg ring-1 ring-secondary">
                        {languages.map((item) => (
                            <li key={item.id} className="flex items-center justify-between gap-3 px-4 py-2.5">
                                <span className="text-sm font-medium text-primary">{item.name}</span>
                                <NativeSelect
                                    aria-label={`CEFR level for ${item.name}`}
                                    value={item.level ?? ""}
                                    disabled={levels.busy === item.id}
                                    onChange={(event) => levels.run(item.id, () => setItemLevel(item.id, event.target.value || null))}
                                    className="w-auto py-1 text-sm"
                                >
                                    <option value="">No level</option>
                                    {cefrLevels.map((level) => (
                                        <option key={level} value={level}>
                                            {level}
                                        </option>
                                    ))}
                                </NativeSelect>
                            </li>
                        ))}
                    </ul>
                )}
                <div className="mt-2">
                    <ErrorText text={levels.error} />
                </div>
            </div>
        </Step>
    );
}
