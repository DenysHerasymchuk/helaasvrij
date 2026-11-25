import { Button } from "@/components/base/buttons/button";
import { useAuth } from "@/core/auth";
import { useEffect, useState } from "react";
import { ErrorState, LoadingState, PageFrame, PageHeader, metaClass, pageTitleClass } from "@/core/ui";
import { isBrandNew, FirstStart, readSkipped } from "./first-start";
import { ActivityFeed } from "./activity-feed";
import { ApplicationsBarChart } from "./applications-chart";
import { BestMatchesCard } from "./best-matches-card";
import { buildFocus } from "./focus";
import { FocusList } from "./focus-list";
import { KpiRow } from "./kpi-row";
import { Panel } from "./panel";
import { PipelineCard } from "./pipeline-card";
import { ProfileCard } from "./profile-card";
import { ShortcutButtons } from "./shortcut-buttons";
import type { DashboardData } from "./use-dashboard";
import { type Home, useHome } from "./use-home";

// Step 1 with nothing yet: no synced source, no posting, no application. Show one pointer instead of empty cards.
const nothingYet = (data: DashboardData) =>
    data.sources.every((s) => !s.synced) && data.postings.length === 0 && data.applicationsByMonth.every((m) => m.applied === 0);

/** Greeting for the local hour: morning until 12, afternoon until 18, evening after. */
function greeting(now = new Date()) {
    const hour = now.getHours();
    return hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
}

/** The whole picture in one screen: what to do today, the numbers, the pipeline, the matches, the profile and the latest moves. */
function Overview({ home }: { home: Home }) {
    const { items } = buildFocus(home);
    const hasApplications = home.dashboard.applicationsByMonth.some((m) => m.applied > 0);

    return (
        <div className="grid min-h-0 flex-1 gap-4 lg:grid-cols-[minmax(0,1fr)_22rem]">
            <div className="flex min-h-0 flex-col gap-4">
                <KpiRow home={home} />
                <FocusList items={items} />
                <div className="grid gap-4 lg:h-80 lg:grid-cols-2">
                    <Panel title="Applications" description={`${home.dashboard.applicationsByMonth.reduce((n, m) => n + m.applied, 0)} sent, ${home.dashboard.applicationsByMonth.reduce((n, m) => n + m.interview, 0)} led to an interview.`} className="min-h-0 overflow-y-auto" order={4}>
                        {hasApplications ? (
                            <ApplicationsBarChart data={home.dashboard.applicationsByMonth} compact />
                        ) : (
                            <p className={`${metaClass} px-5 py-4`}>No applications yet. Move a position to Applied on the board.</p>
                        )}
                    </Panel>
                    <ActivityFeed items={home.activity} />
                </div>
            </div>
            <div className="flex min-h-0 flex-col gap-4 lg:overflow-y-auto">
                <PipelineCard home={home} />
                <BestMatchesCard postings={home.postings} />
                <ProfileCard home={home} />
            </div>
        </div>
    );
}

export function DashboardPage() {
    const { user } = useAuth();
    const firstName = user?.displayName.split(/\s+/)[0] ?? "";
    const hello = greeting();
    const { home, journey: journeyState, error, reload } = useHome();
    const journey = journeyState.journey;
    const [skipped, setSkipped] = useState(readSkipped);
    // Once shown, the start stays for this visit so steps 2 and 3 can follow; a later visit with a source gets the dashboard.
    const [started, setStarted] = useState(false);
    const brandNew = !!journey && isBrandNew(journey);
    useEffect(() => {
        if (brandNew) setStarted(true);
    }, [brandNew]);
    const showStart = !skipped && !!journey && (brandNew || (started && journey.savedPostingCount === 0));

    if (!skipped && !journey && !journeyState.error) {
        return (
            <div className="mx-auto w-full max-w-6xl px-4 py-8 md:px-8">
                <LoadingState>Loading...</LoadingState>
            </div>
        );
    }

    if (showStart && journey) {
        return (
            <div className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-4 py-8 md:px-8">
                <PageHeader title={`${hello}, ${firstName}`} description="Welcome to helaasvrij. Start here." />
                <FirstStart journey={journey} reload={journeyState.reload} onSkip={() => setSkipped(true)} />
            </div>
        );
    }

    return (
        <PageFrame full width="max-w-[1600px]">
            <header className="flex flex-col gap-3">
                <div>
                    <h1 className={pageTitleClass}>
                        {hello}, {firstName}
                    </h1>
                    {home && !nothingYet(home.dashboard) && <p className="mt-1 text-md text-tertiary">{buildFocus(home).sentence}</p>}
                </div>
                <ShortcutButtons />
            </header>

            {error ? (
                <ErrorState message={error} onRetry={reload} />
            ) : !home ? (
                <LoadingState>Loading your home...</LoadingState>
            ) : nothingYet(home.dashboard) ? (
                <div className="grid gap-4 md:grid-cols-2">
                    <ProfileCard home={home} />
                    <section className="dash-card flex flex-col items-start justify-center gap-3 rounded-xl bg-primary p-6 shadow-xs ring-1 ring-secondary">
                        <h2 className="text-md font-semibold text-primary">Start with your profile</h2>
                        <p className="text-sm text-tertiary">
                            Add your GitHub or a CV. We pull your skills and projects from it, with a link to where each one came from. Then you can
                            match it against real postings.
                        </p>
                        <Button size="md" href="/settings/profile">
                            Add your GitHub or CV
                        </Button>
                    </section>
                </div>
            ) : (
                <Overview home={home} />
            )}
        </PageFrame>
    );
}
