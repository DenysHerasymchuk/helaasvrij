import { type ReactNode, useCallback, useEffect, useState } from "react";
import { Dialog, FileTrigger, Heading, Modal, ModalOverlay } from "react-aria-components";
import { FaGithub } from "react-icons/fa";
import { File05, Globe01, RefreshCw01, Trash01, Type01, XClose } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { Input } from "@/components/base/input/input";
import { InputFile } from "@/components/base/input/input-file";
import { ApiError } from "@/core/api";
import { useAuth } from "@/core/auth";
import { Field, TextArea } from "@/features/postings/fields";
import { formatDateTime } from "@/features/postings/types";
import {
    type ExtractResult,
    type Profile,
    type ProfileItem,
    type ProfileItemKind,
    type Source,
    addSite,
    addText,
    deleteProfileItem,
    deleteSource,
    extractProfile,
    getProfile,
    listSources,
    syncGitHub,
    uploadCv,
} from "./api";

const message = (error: unknown) => (error instanceof ApiError ? error.message : "Something went wrong. Try again.");
const plural = (count: number, word: string) => `${count} ${word}${count === 1 ? "" : "s"}`;

const kindOrder: { kind: ProfileItemKind; title: string }[] = [
    { kind: "Skill", title: "Skills" },
    { kind: "Project", title: "Projects" },
    { kind: "Experience", title: "Experience" },
    { kind: "Language", title: "Languages" },
    { kind: "Education", title: "Education" },
    { kind: "Certification", title: "Certifications" },
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

    return (
        <div className="flex w-full flex-col gap-6">
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
                    <div className="grid items-start gap-6 md:grid-cols-2">
                        <GitHubCard source={sources.find((source) => source.kind === "GitHub")} reload={reloadAll} />
                        <SiteCard sources={sources.filter((source) => source.kind === "Site")} reload={reloadAll} />
                    </div>
                    <PdfCard sources={sources.filter((source) => source.kind === "Cv" || source.kind === "PastedText")} reload={reloadAll} />
                    <ReadSources sources={sources} profile={profile} reloadProfile={loadProfile} />
                </>
            )}
        </div>
    );
}

function SourceCard({ icon: Icon, title, status, children }: { icon: typeof Globe01; title: string; status: Source["status"] | null; children: ReactNode }) {
    return (
        <div className="rounded-xl bg-primary p-5 shadow-xs ring-1 ring-secondary">
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

/** The one button: reads every source, then opens a dialog on top with what it found. */
function ReadSources({ sources, profile, reloadProfile }: { sources: Source[]; profile: Profile; reloadProfile: () => Promise<void> }) {
    const [open, setOpen] = useState(false);
    const [reading, setReading] = useState(false);
    const [result, setResult] = useState<ExtractResult | null>(null);
    const [error, setError] = useState<string | null>(null);

    const ready = sources.filter((source) => source.status === "Ready").length;
    const blocked = sources.length === 0 ? "Add GitHub, a CV or your website first." : ready === 0 ? "None of your sources is ready. Fix or remove the failed ones." : null;

    const read = async () => {
        setOpen(true);
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
        <div className="flex flex-col items-start gap-2">
            <Button size="md" isLoading={reading} isDisabled={!!blocked || reading} onPress={read}>
                Read my sources
            </Button>
            {blocked && <p className="text-sm text-tertiary">{blocked}</p>}
            {!blocked && profile.items.length > 0 && !open && (
                <Button color="link-gray" size="sm" onPress={() => setOpen(true)}>
                    See what we found ({plural(profile.items.length, "item")})
                </Button>
            )}

            <ModalOverlay
                isOpen={open}
                onOpenChange={(next) => !next && !reading && setOpen(false)}
                isDismissable
                className="fixed inset-0 z-50 flex items-end justify-center bg-overlay/70 p-4 sm:items-center"
            >
                <Modal className="w-full max-w-2xl">
                    <Dialog className="flex max-h-[85vh] flex-col gap-4 rounded-xl bg-primary p-6 shadow-xl outline-hidden ring-1 ring-secondary">
                        <div className="flex items-start justify-between gap-4">
                            <div>
                                <Heading slot="title" className="text-lg font-semibold text-primary">
                                    What we found
                                </Heading>
                                <p className="mt-1 text-sm text-tertiary">Each item quotes your own source. Your CV, letters and gap plan use all of them, so remove what is wrong.</p>
                            </div>
                            <Button size="sm" color="tertiary" iconLeading={XClose} aria-label="Close" onPress={() => setOpen(false)} isDisabled={reading} />
                        </div>

                        <div role="status" className="flex flex-col gap-1 text-sm empty:hidden">
                            {reading && <p className="text-secondary">Reading {plural(ready, "source")}. This takes up to a minute.</p>}
                            {result && (
                                <p className="text-secondary">
                                    {result.created > 0 ? `Found ${plural(result.created, "new item")}.` : "No new items. Everything we can prove is already listed."}
                                    {result.skippedDocuments > 0 && ` We skipped ${plural(result.skippedDocuments, "document")} to stay within limits; read again to include them.`}
                                </p>
                            )}
                            {result && result.dropped > 0 && (
                                <p className="text-warning-primary">We left out {plural(result.dropped, "claim")} because the quote was not in your source.</p>
                            )}
                        </div>
                        <ErrorText text={error} />

                        <div className="-mx-2 min-h-0 flex-1 overflow-y-auto px-2">
                            {reading ? (
                                <div aria-hidden="true" className="h-32 animate-pulse rounded-lg bg-tertiary" />
                            ) : (
                                <ItemsList profile={profile} reloadProfile={reloadProfile} />
                            )}
                        </div>
                    </Dialog>
                </Modal>
            </ModalOverlay>
        </div>
    );
}

function ItemsList({ profile, reloadProfile }: { profile: Profile; reloadProfile: () => Promise<void> }) {
    if (profile.items.length === 0) return <p className="text-sm text-tertiary">Nothing found yet.</p>;

    return (
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
    );
}

function ItemCard({ item, reload }: { item: ProfileItem; reload: () => Promise<void> }) {
    const { busy, error, run } = useRunner(reload);

    return (
        <li className="flex flex-col gap-3 rounded-lg p-4 ring-1 ring-secondary">
            <div className="flex flex-wrap items-center gap-2">
                <span className="text-md font-semibold text-primary">{item.name}</span>
                <div className="ml-auto flex gap-2">
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
