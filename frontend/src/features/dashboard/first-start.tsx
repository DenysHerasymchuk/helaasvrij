import { type ReactNode, useState } from "react";
import { ArrowRight, Check } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { Input } from "@/components/base/input/input";
import { InputFile } from "@/components/base/input/input-file";
import { ApiError } from "@/core/api";
import { cardClass, itemsInCard } from "@/core/ui";
import type { Journey } from "@/features/journey/journey-bar";
import { type Source, extractProfile, syncGitHub, uploadCv } from "@/features/profile/api";
import { cx } from "@/utils/cx";

const SKIP_KEY = "coach.skipFirstStart";

export function readSkipped() {
    try {
        return localStorage.getItem(SKIP_KEY) === "1";
    } catch {
        return false;
    }
}

function saveSkipped() {
    try {
        localStorage.setItem(SKIP_KEY, "1");
    } catch {
        // Storage blocked: the skip lasts until the page reloads.
    }
}

/** A brand-new account: nothing read yet and no posting saved. */
export const isBrandNew = (j: Journey) => j.currentStep === 1 && !j.hasSources && j.savedPostingCount === 0;

const message = (e: unknown) => (e instanceof ApiError ? e.message : "Something went wrong. Try again.");

function Step({ n, title, done, current, children }: { n: number; title: string; done: boolean; current: boolean; children?: ReactNode }) {
    return (
        <li aria-current={current ? "step" : undefined} className="flex gap-4 px-4 py-5 md:px-6">
            <span
                aria-hidden="true"
                className={cx(
                    "flex size-8 shrink-0 items-center justify-center rounded-full text-sm font-semibold",
                    done && "bg-brand-solid text-white",
                    current && "bg-primary text-brand-secondary ring-2 ring-brand",
                    !done && !current && "bg-primary text-quaternary ring-1 ring-secondary",
                )}
            >
                {done ? <Check className="size-4" /> : n}
            </span>
            <div className="flex min-w-0 flex-1 flex-col gap-3 pt-1">
                <h2 className={cx("text-md font-semibold", current ? "text-primary" : done ? "text-secondary" : "text-tertiary")}>
                    {title}
                    {done && <span className="sr-only"> (done)</span>}
                </h2>
                {children}
            </div>
        </li>
    );
}

/** Three steps for a first sign-in, in place of the dashboard cards. */
export function FirstStart({ journey, reload, onSkip }: { journey: Journey; reload: () => Promise<void>; onSkip: () => void }) {
    const [username, setUsername] = useState("");
    const [busy, setBusy] = useState<"github" | "cv" | "read" | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [read, setRead] = useState<number | null>(null);

    const hasSource = journey.hasSources;
    const confirmed = journey.confirmedItemCount > 0;
    const picked = journey.savedPostingCount > 0;

    async function addSource(key: "github" | "cv", action: () => Promise<Source | null>) {
        setBusy(key);
        setError(null);
        try {
            const source = await action();
            if (source?.status === "Failed") setError(source.error ?? "We could not read that source. Try another one.");
            await reload();
        } catch (e) {
            setError(message(e));
        } finally {
            setBusy(null);
        }
    }

    async function readSources() {
        setBusy("read");
        setError(null);
        try {
            const result = await extractProfile();
            setRead(result?.created ?? 0);
            await reload();
        } catch (e) {
            setError(message(e));
        } finally {
            setBusy(null);
        }
    }

    const errorText = error && (
        <p role="alert" className="text-sm text-error-primary">
            {error}
        </p>
    );

    return (
        <section aria-labelledby="first-start-title" className={cardClass}>
            <header className="border-b border-secondary px-4 py-5 md:px-6">
                <h2 id="first-start-title" className="text-lg font-semibold text-primary">
                    Three steps to your first match
                </h2>
                <p className="mt-1 text-sm text-tertiary">We read your work, you confirm what is true, then we compare it with a real posting.</p>
            </header>

            <ol className={itemsInCard}>
                <Step n={1} title="Add your GitHub or CV" done={hasSource} current={!hasSource}>
                    {!hasSource && (
                        <>
                            <form
                                className="flex flex-col gap-2 sm:flex-row sm:items-start"
                                onSubmit={(event) => {
                                    event.preventDefault();
                                    void addSource("github", () => syncGitHub(username.trim()));
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
                                <Button type="submit" size="md" isLoading={busy === "github"} isDisabled={!username.trim() || busy !== null} className="self-start sm:mt-6">
                                    Add
                                </Button>
                            </form>
                            <InputFile
                                size="md"
                                label="Or upload your CV or LinkedIn PDF"
                                placeholder="Choose a PDF"
                                buttonText="Upload PDF"
                                acceptedFileTypes={["application/pdf", ".pdf"]}
                                isLoading={busy === "cv"}
                                isDisabled={busy !== null}
                                hint="PDF up to 5 MB. From LinkedIn: open your profile, More, Save to PDF."
                                onChange={(files) => {
                                    const file = files?.[0];
                                    if (file) void addSource("cv", () => uploadCv(file));
                                }}
                            />
                            {busy === "github" && (
                                <p role="status" className="text-sm text-tertiary">
                                    Reading your repositories. This can take a few seconds.
                                </p>
                            )}
                            {errorText}
                        </>
                    )}
                </Step>

                <Step n={2} title="Read and confirm" done={confirmed} current={hasSource && !confirmed}>
                    {hasSource && !confirmed && (
                        <>
                            <p className="text-sm text-tertiary">
                                {read === null
                                    ? "We pull your skills and projects from your sources, each with a link to where it came from."
                                    : `Found ${read} new item${read === 1 ? "" : "s"}. Tick the ones that are true on your profile.`}
                            </p>
                            <div className="flex flex-wrap gap-2">
                                {read === null && (
                                    <Button size="md" isLoading={busy === "read"} isDisabled={busy !== null} onPress={readSources}>
                                        Read my sources
                                    </Button>
                                )}
                                <Button size="md" color={read === null ? "secondary" : "primary"} href="/profile#step-3" iconTrailing={ArrowRight}>
                                    Confirm on your profile
                                </Button>
                            </div>
                            {errorText}
                        </>
                    )}
                </Step>

                <Step n={3} title="Pick a posting" done={picked} current={confirmed && !picked}>
                    {confirmed && !picked && (
                        <>
                            <p className="text-sm text-tertiary">Search Belgian internships or paste one you found. We show what it asks for that your profile does not.</p>
                            <Button size="md" href="/postings" iconTrailing={ArrowRight} className="self-start">
                                Find internships
                            </Button>
                        </>
                    )}
                </Step>
            </ol>

            <footer className="border-t border-secondary px-4 py-3 md:px-6">
                <Button
                    color="link-gray"
                    size="sm"
                    onPress={() => {
                        saveSkipped();
                        onSkip();
                    }}
                >
                    Skip, show my dashboard
                </Button>
            </footer>
        </section>
    );
}
