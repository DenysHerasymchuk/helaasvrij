import { File06, FileCheck02, MessageChatCircle, Plus } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { useAuth } from "@/core/auth";
import { useEffect, useState } from "react";
import { useJourney } from "@/features/journey/journey-bar";
import { ActiveSprintCard } from "./sprint-card";
import { FirstStart, isBrandNew, readSkipped } from "./first-start";
import { ApplicationsBarChart } from "./applications-chart";
import { BestMatchesCard } from "./best-matches-card";
import { ComingUpCard } from "./coming-up-card";
import { NextStepsCard } from "./next-steps-card";
import { HeaderLink, Panel } from "./panel";
import { SourcesCard } from "./sources-card";
import { type DashboardData, useDashboard } from "./use-dashboard";
import { PageHeader, ErrorState, LoadingState } from "@/core/ui";

// Step 1 with nothing yet: no synced source, no posting, no application. Show one pointer instead of empty cards.
const nothingYet = (data: DashboardData) =>
    data.sources.every((s) => !s.synced) && data.postings.length === 0 && data.applicationsByMonth.every((m) => m.applied === 0);

/** Greeting for the local hour: morning until 12, afternoon until 18, evening after. */
function greeting(now = new Date()) {
    const hour = now.getHours();
    return hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
}

export function DashboardPage() {
    const { user } = useAuth();
    const firstName = user?.displayName.split(/\s+/)[0] ?? "";
    const hello = greeting();
    const { data, error, reload } = useDashboard();
    const journey = useJourney();
    const [skipped, setSkipped] = useState(readSkipped);
    // Once shown, the start stays for this visit so steps 2 and 3 can follow; a later visit with a source gets the dashboard.
    const [started, setStarted] = useState(false);
    const brandNew = !!journey.journey && isBrandNew(journey.journey);
    useEffect(() => {
        if (brandNew) setStarted(true);
    }, [brandNew]);
    const showStart = !skipped && !!journey.journey && (brandNew || (started && journey.journey.savedPostingCount === 0));

    if (!skipped && !journey.journey && !journey.error) {
        return (
            <div className="mx-auto w-full max-w-6xl px-4 py-8 md:px-8">
                <LoadingState>Loading...</LoadingState>
            </div>
        );
    }

    if (showStart && journey.journey) {
        return (
            <div className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-4 py-8 md:px-8">
                <PageHeader title={`${hello}, ${firstName}`} description="Welcome to helaasvrij. Start here." />
                <FirstStart journey={journey.journey} reload={journey.reload} onSkip={() => setSkipped(true)} />
            </div>
        );
    }

    return (
        <div className="mx-auto flex w-full max-w-6xl flex-col gap-6 px-4 py-8 md:px-8">
            <div className="flex flex-col gap-4">
                <PageHeader title={`${hello}, ${firstName}`} />
                <ActiveSprintCard />
                <nav aria-label="Quick actions" className="flex flex-wrap gap-3">
                    <Button size="md" href="/backlog" iconLeading={Plus}>
                        Add position
                    </Button>
                    <Button size="md" color="secondary" href="/documents" iconLeading={FileCheck02}>
                        Tailor CV
                    </Button>
                    <Button size="md" color="secondary" href="/documents" iconLeading={File06}>
                        Prepare documents
                    </Button>
                    <Button size="md" color="secondary" href="/interview" iconLeading={MessageChatCircle}>
                        Practice interview
                    </Button>
                </nav>
            </div>

            {error ? (
                <ErrorState message={error} onRetry={reload} />
            ) : !data ? (
                <LoadingState>Loading your dashboard...</LoadingState>
            ) : nothingYet(data) ? (
                <div className="grid gap-6 md:grid-cols-2">
                    <SourcesCard sources={data.sources} />
                    <section className="dash-card flex flex-col items-start justify-center gap-3 rounded-xl bg-primary p-6 shadow-xs ring-1 ring-secondary">
                        <h2 className="text-md font-semibold text-primary">Start with your profile</h2>
                        <p className="text-sm text-tertiary">
                            Add your GitHub or a CV. We pull your skills and projects from it, with a link to where each one came from. Then you can
                            match it against real postings.
                        </p>
                        <Button size="md" href="/profile">
                            Add your GitHub or CV
                        </Button>
                    </section>
                </div>
            ) : (
                <>
                    <Panel
                        title="Applications"
                        description="Sent per month, and how many led to an interview or an offer."
                        order={1}
                        action={<HeaderLink href="/sprints">Sprints</HeaderLink>}
                    >
                        {data.applicationsByMonth.some((m) => m.applied > 0) ? (
                            <ApplicationsBarChart data={data.applicationsByMonth} />
                        ) : (
                            <p className="px-5 py-6 text-sm text-tertiary">No applications yet. Start a sprint and move positions across the board.</p>
                        )}
                    </Panel>

                    <div className="grid gap-6 md:grid-cols-2">
                        <SourcesCard sources={data.sources} />
                        <ComingUpCard postings={data.postings} />
                    </div>

                    <div className="grid gap-6 lg:grid-cols-5">
                        <BestMatchesCard postings={data.postings} />
                        <NextStepsCard postings={data.postings} />
                    </div>
                </>
            )}
        </div>
    );
}
