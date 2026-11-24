import { File06, FileCheck02, MessageChatCircle, Plus } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { useAuth } from "@/core/auth";
import { ApplicationsBarChart } from "./applications-chart";
import { BestMatchesCard } from "./best-matches-card";
import { ComingUpCard } from "./coming-up-card";
import { NextStepsCard } from "./next-steps-card";
import { HeaderLink, Panel } from "./panel";
import { SourcesCard } from "./sources-card";
import { useDashboard } from "./use-dashboard";

export function DashboardPage() {
    const { user } = useAuth();
    const firstName = user?.displayName.split(/\s+/)[0] ?? "";
    const { data, error, reload } = useDashboard();

    return (
        <div className="mx-auto flex max-w-6xl flex-col gap-6 px-4 py-8 md:px-8">
            <div className="flex flex-col gap-4">
                <h1 className="text-display-xs font-semibold text-primary">Goeiemorgen, {firstName}</h1>
                <nav aria-label="Quick actions" className="flex flex-wrap gap-3">
                    <Button size="md" href="/postings" iconLeading={Plus}>
                        Add posting
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
                <div role="alert" className="flex flex-col items-start gap-3 rounded-xl bg-primary p-5 shadow-xs ring-1 ring-secondary">
                    <p className="text-sm text-secondary">{error}</p>
                    <Button size="md" color="secondary" onPress={reload}>
                        Try again
                    </Button>
                </div>
            ) : !data ? (
                <p role="status" className="text-sm text-tertiary">
                    Loading your dashboard...
                </p>
            ) : (
                <>
                    <Panel
                        title="Applications"
                        description="Sent per month, and how many led to an interview or an offer."
                        order={1}
                        action={<HeaderLink href="/applications">See all</HeaderLink>}
                    >
                        {data.applicationsByMonth.some((m) => m.applied > 0) ? (
                            <ApplicationsBarChart data={data.applicationsByMonth} />
                        ) : (
                            <p className="px-5 py-6 text-sm text-tertiary">No applications yet. Track a posting on the Applications board.</p>
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
