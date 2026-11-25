import { useEffect, useState } from "react";
import { NavLink, Outlet, useLocation, useOutletContext } from "react-router";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { ErrorState, PageHeader } from "@/core/ui";
import { cx } from "@/utils/cx";

export type Language = "nl" | "fr" | "en";
export type Settings = {
    defaultLanguage: Language;
    strongMatchThreshold: number;
    emailAlerts: boolean;
    /** Writing skill ids the AI uses for CVs, letters and messages. */
    documentSkills: string[];
    /** Writing skill ids the AI uses for interview questions and feedback. */
    interviewSkills: string[];
};
export type SettingsContext = { draft: Settings; update: (change: Partial<Settings>) => void };
export const useSettings = () => useOutletContext<SettingsContext>();

const tabs = [
    { to: "preferences", label: "Preferences", saves: true },
    { to: "profile", label: "Profile", saves: false },
    { to: "notifications", label: "Notifications", saves: true },
    { to: "account", label: "Account", saves: false },
    { to: "data", label: "Data and privacy", saves: false },
];

/** Loads settings once; Preferences and Notifications edit one draft saved by a shared bar. */
export function SettingsLayout() {
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
        <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 py-8 md:px-8">
            <PageHeader title="Settings" description="Your profile sources, defaults for matches and drafts, and control over your data." />

            <nav aria-label="Settings sections" className="-mb-px flex gap-6 overflow-x-auto border-b border-secondary [scrollbar-width:none]">
                {tabs.map((tab) => (
                    <NavLink
                        key={tab.to}
                        to={tab.to}
                        viewTransition
                        className={({ isActive }) =>
                            cx(
                                "-mb-px shrink-0 border-b-2 pb-3 text-sm font-medium whitespace-nowrap outline-focus-ring focus-visible:outline-2 focus-visible:outline-offset-4",
                                isActive ? "border-brand-solid text-primary" : "border-transparent text-tertiary hover:text-primary",
                            )
                        }
                    >
                        {tab.label}
                    </NavLink>
                ))}
            </nav>

            {loadError ? (
                <ErrorState message={loadError} onRetry={load} />
            ) : !draft ? (
                <div aria-busy="true" aria-label="Loading settings" className="h-40 animate-pulse rounded-xl bg-tertiary" />
            ) : (
                <>
                    <Outlet context={{ draft, update } satisfies SettingsContext} />
                    <SaveBar dirty={dirty} saveState={saveState} saveError={saveError} onSave={save} />
                </>
            )}
        </div>
    );
}

/** Shown on every tab that edits the draft; hidden on Account and Data. */
function SaveBar({ dirty, saveState, saveError, onSave }: { dirty: boolean; saveState: string; saveError: string | null; onSave: () => void }) {
    const { pathname: path } = useLocation();
    if (!tabs.some((tab) => tab.saves && path.endsWith(`/${tab.to}`))) return null;
    return (
        <div className="flex flex-wrap items-center gap-3">
            <Button color="primary" size="md" isDisabled={!dirty} isLoading={saveState === "saving"} onClick={onSave}>
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
    );
}
