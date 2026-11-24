import { ArrowUpRight, Calendar, CheckCircle, FileCheck02, Globe01 } from "@untitledui/icons";
import { FaGithub } from "react-icons/fa6";
import { Button } from "@/components/base/buttons/button";
import { cx } from "@/utils/cx";
import { useAuth } from "@/core/auth";
import { ApplicationsBarChart, CoverageDonut, ScoreRing } from "./charts";
import { applicationsByMonth, demoUser, impact, postings, score } from "@/features/demo/demo-user";

const dateFmt = new Intl.DateTimeFormat("en-BE", { day: "numeric", month: "short" });
const fmt = (iso: string) => dateFmt.format(new Date(iso));

const ranked = [...postings].sort((a, b) => score(b.requirements) - score(a.requirements));

const actions = postings
    .flatMap((p) => p.requirements.filter((r) => r.action && impact(r) > 0).map((r) => ({ ...r, company: p.company })))
    .sort((a, b) => impact(b) - impact(a) || (a.due ?? "").localeCompare(b.due ?? ""));

const nextDeadline = [...postings].sort((a, b) => a.deadline.localeCompare(b.deadline))[0];
const nextInterview = postings.find((p) => p.interviewAt);

function Panel({ title, action, children, className }: { title: string; action?: React.ReactNode; children: React.ReactNode; className?: string }) {
    return (
        <section className={cx("rounded-xl bg-primary shadow-xs ring-1 ring-secondary", className)}>
            <header className="flex items-center justify-between gap-4 border-b border-secondary px-5 py-4">
                <h2 className="text-md font-semibold text-primary">{title}</h2>
                {action}
            </header>
            {children}
        </section>
    );
}

export function DashboardPage() {
    const { user } = useAuth();
    const firstName = user?.displayName.split(/\s+/)[0] ?? "";

    return (
        <div className="mx-auto flex max-w-6xl flex-col gap-6 px-4 py-8 md:px-8">
            <h1 className="text-display-xs font-semibold text-primary">Goeiemorgen, {firstName}</h1>

            <div className="grid gap-6 lg:grid-cols-3">
                <Panel title="Requirements across your postings">
                    <CoverageDonut postings={postings} />
                </Panel>
                <Panel title="Applications and results" className="lg:col-span-2">
                    <ApplicationsBarChart data={applicationsByMonth} />
                </Panel>
            </div>

            <div className="grid gap-6 md:grid-cols-2">

                <Panel title="Sources">
                    <ul className="flex flex-col divide-y divide-secondary">
                        {demoUser.sources.map((s) => {
                            const Icon = s.kind === "GitHub" ? FaGithub : s.kind === "CV" ? FileCheck02 : Globe01;
                            return (
                                <li key={s.kind} className="flex items-center gap-3 px-5 py-3">
                                    <Icon className="size-5 shrink-0 text-fg-secondary" aria-hidden="true" />
                                    <div className="min-w-0 flex-1">
                                        <p className="text-sm font-medium text-primary">{s.kind}</p>
                                        <p className="truncate text-sm text-tertiary">{s.label}</p>
                                    </div>
                                    {s.synced ? (
                                        <CheckCircle className="size-5 text-fg-success-secondary" aria-label="Connected" />
                                    ) : (
                                        <Button color="link-color" size="sm" href="/profile">
                                            Add
                                        </Button>
                                    )}
                                </li>
                            );
                        })}
                    </ul>
                </Panel>

                <Panel title="Coming up">
                    <ul className="flex flex-col divide-y divide-secondary">
                        {nextInterview?.interviewAt && (
                            <li className="flex gap-3 px-5 py-4">
                                <Calendar className="mt-0.5 size-5 shrink-0 text-fg-brand-secondary" aria-hidden="true" />
                                <div>
                                    <p className="text-sm font-medium text-primary">Interview at {nextInterview.company}</p>
                                    <p className="text-sm text-tertiary">
                                        {fmt(nextInterview.interviewAt)}, {nextInterview.interviewAt.slice(11)}
                                    </p>
                                </div>
                            </li>
                        )}
                        <li className="flex gap-3 px-5 py-4">
                            <Calendar className="mt-0.5 size-5 shrink-0 text-fg-quaternary" aria-hidden="true" />
                            <div>
                                <p className="text-sm font-medium text-primary">Deadline {nextDeadline.company}</p>
                                <p className="text-sm text-tertiary">
                                    {fmt(nextDeadline.deadline)}, {nextDeadline.title}
                                </p>
                            </div>
                        </li>
                    </ul>
                </Panel>
            </div>

            <div className="grid gap-6 lg:grid-cols-5">
                <Panel
                    title="Best matches"
                    className="lg:col-span-3"
                    action={
                        <Button color="link-gray" size="sm" href="/postings" iconTrailing={ArrowUpRight}>
                            All postings
                        </Button>
                    }
                >
                    <ul className="flex flex-col divide-y divide-secondary">
                        {ranked.slice(0, 3).map((p) => (
                            <li key={p.id} className="flex items-center gap-5 px-5 py-4">
                                <ScoreRing value={score(p.requirements)} />
                                <div className="flex min-w-0 flex-1 flex-col gap-1">
                                    <p className="truncate text-sm font-semibold text-primary">{p.title}</p>
                                    <p className="text-sm text-tertiary">
                                        {p.company}, {p.city}. Apply by {fmt(p.deadline)}
                                    </p>
                                </div>
                            </li>
                        ))}
                    </ul>
                    <p className="border-t border-secondary px-5 py-3 text-xs text-tertiary">Score: required skills count twice, nice-to-have once. Green from 75%, orange from 50%.</p>
                </Panel>

                <Panel
                    title="Gap plan"
                    className="lg:col-span-2"
                    action={
                        <Button color="link-gray" size="sm" href="/gaps" iconTrailing={ArrowUpRight}>
                            Open plan
                        </Button>
                    }
                >
                    <ol className="flex flex-col divide-y divide-secondary">
                        {actions.slice(0, 5).map((a) => (
                            <li key={a.action} className="flex flex-col gap-1 px-5 py-3">
                                <p className="text-sm font-medium text-primary">{a.action}</p>
                                <p className="text-sm text-tertiary">
                                    <span className="font-mono text-secondary">{a.project}</span> for {a.company}. Due {fmt(a.due!)}
                                </p>
                            </li>
                        ))}
                    </ol>
                </Panel>
            </div>

        </div>
    );
}
