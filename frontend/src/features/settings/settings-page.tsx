import { type ReactNode, useEffect, useState } from "react";
import { Dialog, Modal, ModalOverlay, Switch } from "react-aria-components";
import { useNavigate } from "react-router";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { Input } from "@/components/base/input/input";
import { RadioButton, RadioGroup } from "@/components/base/radio-buttons/radio-buttons";
import { ApiError, api } from "@/core/api";
import { useAuth } from "@/core/auth";
import { cx } from "@/utils/cx";
import { PageHeader } from "@/core/ui";

type Language = "nl" | "fr" | "en";
type Settings = { defaultLanguage: Language; strongMatchThreshold: number; emailAlerts: boolean };

const languages: { value: Language; label: string; hint: string }[] = [
    { value: "nl", label: "Nederlands", hint: "Documents and drafts default to Dutch." },
    { value: "fr", label: "Français", hint: "Documents and drafts default to French." },
    { value: "en", label: "English", hint: "Documents and drafts default to English." },
];

function Section({ title, description, children, tone = "default" }: { title: string; description: string; children: ReactNode; tone?: "default" | "danger" }) {
    return (
        <section className={cx("rounded-xl bg-primary shadow-xs ring-1", tone === "danger" ? "ring-error_subtle" : "ring-secondary")}>
            <header className="border-b border-secondary px-5 py-4">
                <h2 className="text-md font-semibold text-primary">{title}</h2>
                <p className="mt-0.5 text-sm text-tertiary">{description}</p>
            </header>
            <div className="px-5 py-5">{children}</div>
        </section>
    );
}

export function SettingsPage() {
    const [saved, setSaved] = useState<Settings | null>(null);
    const [draft, setDraft] = useState<Settings | null>(null);
    const [loadError, setLoadError] = useState<string | null>(null);
    const [saveState, setSaveState] = useState<"idle" | "saving" | "saved">("idle");
    const [saveError, setSaveError] = useState<string | null>(null);

    const load = () => {
        setLoadError(null);
        api.get<Settings>("/api/settings")
            .then((settings) => {
                setSaved(settings);
                setDraft(settings);
            })
            .catch((error: unknown) => setLoadError(error instanceof ApiError ? error.message : "Something went wrong. Try again."));
    };

    useEffect(load, []);

    const save = async () => {
        if (!draft) return;
        setSaveState("saving");
        setSaveError(null);
        try {
            const result = await api.put<Settings>("/api/settings", draft);
            if (result) {
                setSaved(result);
                setDraft(result);
            }
            setSaveState("saved");
        } catch (error) {
            setSaveError(error instanceof ApiError ? error.message : "Something went wrong. Try again.");
            setSaveState("idle");
        }
    };

    const dirty = saved !== null && draft !== null && JSON.stringify(saved) !== JSON.stringify(draft);
    const update = (change: Partial<Settings>) => {
        setDraft((current) => (current ? { ...current, ...change } : current));
        setSaveState("idle");
    };

    return (
        <div className="flex max-w-3xl flex-col gap-6 px-4 py-8 md:px-8">
            <PageHeader title="Settings" description="Defaults for your matches and drafts, and control over your data." />

            {loadError ? (
                <div role="alert" className="flex flex-col items-start gap-3 rounded-xl bg-primary p-5 shadow-xs ring-1 ring-secondary">
                    <p className="text-sm text-error-primary">{loadError}</p>
                    <Button color="secondary" size="sm" onClick={load}>
                        Try again
                    </Button>
                </div>
            ) : !draft ? (
                <div aria-busy="true" aria-label="Loading settings" className="flex flex-col gap-6">
                    {[0, 1, 2].map((key) => (
                        <div key={key} className="h-40 animate-pulse rounded-xl bg-tertiary" />
                    ))}
                </div>
            ) : (
                <>
                    <Section title="Language" description="The language used for new documents when a posting does not set one.">
                        <RadioGroup
                            aria-label="Default language"
                            size="md"
                            value={draft.defaultLanguage}
                            onChange={(value) => update({ defaultLanguage: value as Language })}
                        >
                            {languages.map((language) => (
                                <RadioButton key={language.value} value={language.value} label={language.label} hint={language.hint} />
                            ))}
                        </RadioGroup>
                    </Section>

                    <Section title="Strong match threshold" description="Postings at or above this score count as a strong match.">
                        <div className="flex items-center gap-4">
                            <input
                                type="range"
                                min={0}
                                max={100}
                                step={5}
                                value={draft.strongMatchThreshold}
                                onChange={(event) => update({ strongMatchThreshold: Number(event.target.value) })}
                                aria-label="Strong match threshold"
                                className="h-2 flex-1 cursor-pointer accent-brand-600 dark:accent-brand-400 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-focus-ring"
                            />
                            <output className="w-14 text-right text-lg font-semibold text-primary tabular-nums">{draft.strongMatchThreshold}%</output>
                        </div>
                    </Section>

                    <Section title="Email alerts" description="Get an email when a new posting is a strong match.">
                        <div className="flex items-center justify-between gap-4">
                            <div className="flex items-center gap-2">
                                <span className="text-sm font-medium text-secondary">Email me about strong matches</span>
                                <Badge type="pill-color" color="gray" size="sm">
                                    Coming soon
                                </Badge>
                            </div>
                            <Switch
                                isSelected={draft.emailAlerts}
                                onChange={(value) => update({ emailAlerts: value })}
                                aria-label="Email alerts"
                                className="group inline-flex cursor-pointer"
                            >
                                <span className="flex h-5 w-9 items-center rounded-full bg-quaternary p-0.5 transition group-data-[selected]:bg-brand-solid group-data-[focus-visible]:outline-2 group-data-[focus-visible]:outline-offset-2 group-data-[focus-visible]:outline-focus-ring">
                                    <span className="size-4 rounded-full bg-fg-white shadow-sm transition group-data-[selected]:translate-x-4" />
                                </span>
                            </Switch>
                        </div>
                        <p className="mt-2 text-sm text-tertiary">Saved now, but no emails are sent yet.</p>
                    </Section>

                    <div className="flex flex-wrap items-center gap-3">
                        <Button color="primary" size="md" isDisabled={!dirty} isLoading={saveState === "saving"} onClick={save}>
                            Save changes
                        </Button>
                        <span role="status" className="text-sm text-tertiary">
                            {saveState === "saved" && !dirty ? "Saved." : ""}
                        </span>
                        {saveError && (
                            <span role="alert" className="text-sm text-error-primary">
                                {saveError}
                            </span>
                        )}
                    </div>
                </>
            )}

            <AccountSection />
        </div>
    );
}

function AccountSection() {
    const { logout } = useAuth();
    const navigate = useNavigate();
    const [signingOut, setSigningOut] = useState(false);

    const signOut = async () => {
        setSigningOut(true);
        try {
            await logout();
        } finally {
            navigate("/login", { replace: true });
        }
    };

    return (
        <>
            <Section title="Account" description="Sign out of this browser.">
                <Button color="secondary" size="md" isLoading={signingOut} onClick={signOut}>
                    Sign out
                </Button>
            </Section>
            <DangerZone />
        </>
    );
}

function DangerZone() {
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
        <Section tone="danger" title="Danger zone" description="Removes your profile, sources, postings, drafts and settings, and your account. This cannot be undone.">
            <Button color="primary-destructive" size="md" onClick={() => setOpen(true)}>
                Delete all my data
            </Button>

            <ModalOverlay
                isOpen={open}
                onOpenChange={(next) => !next && close()}
                isDismissable={!deleting}
                className="fixed inset-0 z-50 flex items-end justify-center bg-overlay/70 p-4 sm:items-center"
            >
                <Modal className="w-full max-w-md outline-hidden">
                    <Dialog aria-label="Delete all my data" className="flex flex-col gap-4 rounded-xl bg-primary p-6 shadow-xl outline-hidden">
                        <div>
                            <h2 className="text-lg font-semibold text-primary">Delete all my data?</h2>
                            <p className="mt-1 text-sm text-tertiary">
                                Everything you own in Internship Coach is deleted and you are signed out. You cannot sign in with these credentials afterwards.
                            </p>
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
        </Section>
    );
}
