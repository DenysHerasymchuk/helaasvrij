import { useCallback, useEffect, useState } from "react";
import { Link, useLocation } from "react-router";
import { ArrowRight, Check } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { cx } from "@/utils/cx";

export type Journey = {
    /** 1 build profile, 2 pick a posting, 3 close gaps, 4 apply and track. */
    currentStep: 1 | 2 | 3 | 4;
    /** True once the best posting is marked applied. */
    complete: boolean;
    hasSources: boolean;
    confirmedItemCount: number;
    hasStagePeriod: boolean;
    savedPostingCount: number;
    analysedPostingCount: number;
    bestPosting: { id: string; company: string; title: string; score: number } | null;
    openGapCount: number;
    documentCountForBest: number;
    bestOnBoard: boolean;
    bestApplied: boolean;
    onBoardCount: number;
    appliedCount: number;
    nextAction: { label: string; href: string };
};

const steps = [
    { name: "Build profile", href: "/profile" },
    { name: "Pick a posting", href: "/postings" },
    { name: "Close gaps", href: "/gaps" },
    { name: "Apply & track", href: "/applications" },
];

/** Loads GET /api/journey again on every navigation, so the bar follows what the student just did. */
export function useJourney() {
    const { key } = useLocation();
    const [journey, setJourney] = useState<Journey | null>(null);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setError(null);
        try {
            setJourney(await api.get<Journey>("/api/journey"));
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Something went wrong. Try again.");
        }
    }, []);

    useEffect(() => {
        load();
    }, [load, key]);

    return { journey, error, reload: load };
}

/** The four steps to an application, with the one most useful next thing to do. */
export function JourneyBar({ className }: { className?: string }) {
    const { journey, error, reload } = useJourney();

    if (error) {
        return (
            <div role="alert" className={cx("flex flex-wrap items-center gap-3 rounded-xl bg-primary px-4 py-3 shadow-xs ring-1 ring-secondary", className)}>
                <p className="text-sm text-secondary">Could not load your next step.</p>
                <Button size="sm" color="secondary" onPress={reload}>
                    Try again
                </Button>
            </div>
        );
    }

    return (
        <nav
            aria-label="Your path to an internship"
            aria-busy={!journey}
            className={cx(
                "flex flex-col gap-4 rounded-xl bg-primary px-4 py-4 shadow-xs ring-1 ring-secondary md:flex-row md:items-center md:justify-between md:px-5",
                className,
            )}
        >
            <ol className="grid flex-1 grid-cols-4 gap-2 md:max-w-xl">
                {steps.map((step, i) => {
                    const n = i + 1;
                    const done = !!journey && (n < journey.currentStep || journey.complete);
                    const current = !!journey && !done && n === journey.currentStep;
                    return (
                        <li key={step.href} className="relative flex min-w-0">
                            {i > 0 && (
                                <span
                                    aria-hidden="true"
                                    className={cx(
                                        "absolute top-3.5 right-[calc(50%+18px)] left-[calc(-50%+14px)] h-0.5 rounded-full",
                                        done || current ? "bg-brand-solid" : "bg-quaternary",
                                    )}
                                />
                            )}
                            <Link
                                to={step.href}
                                aria-current={current ? "step" : undefined}
                                className="group flex w-full flex-col items-center gap-1.5 rounded-md px-1 py-0.5 text-center outline-focus-ring focus-visible:outline-2 focus-visible:outline-offset-2"
                            >
                                <span
                                    className={cx(
                                        "relative flex size-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold transition-colors",
                                        done && "bg-brand-solid text-white",
                                        current && "bg-primary text-brand-secondary ring-2 ring-brand",
                                        !done && !current && "bg-primary text-quaternary ring-1 ring-secondary group-hover:ring-primary",
                                    )}
                                >
                                    {done ? <Check aria-hidden="true" className="size-4" /> : n}
                                </span>
                                <span
                                    className={cx(
                                        "text-xs leading-tight font-medium group-hover:underline",
                                        current ? "text-primary" : done ? "text-secondary" : "text-tertiary",
                                    )}
                                >
                                    {step.name}
                                    {done && <span className="sr-only"> (done)</span>}
                                </span>
                            </Link>
                        </li>
                    );
                })}
            </ol>

            {journey ? (
                <Button size="md" href={journey.nextAction.href} iconTrailing={ArrowRight} className="w-full shrink-0 md:w-auto">
                    Next: {journey.nextAction.label}
                </Button>
            ) : (
                <div aria-hidden="true" className="h-10 w-full shrink-0 animate-pulse rounded-lg bg-secondary md:w-56" />
            )}
        </nav>
    );
}
