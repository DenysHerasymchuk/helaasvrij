import { ArrowUpRight, Calendar, CheckCircle, FileCheck02, GitBranch01, Globe01 } from "@untitledui/icons";
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ChartTooltipContent } from "@/components/application/charts/charts-base";
import { Badge, BadgeWithDot } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { cx } from "@/utils/cx";
import { useAuth } from "@/core/auth";
import { type Coverage, type Posting, demoUser, impact, postings, score, trend } from "@/features/demo/demo-user";

const dateFmt = new Intl.DateTimeFormat("en-BE", { day: "numeric", month: "short" });
const fmt = (iso: string) => dateFmt.format(new Date(iso));

const coverageColor: Record<Coverage, string> = {
    strong: "bg-success-solid",
    partial: "bg-warning-solid",
    none: "bg-quaternary",
};

const ranked = [...postings].sort((a, b) => score(b.requirements) - score(a.requirements));

const actions = postings
    .flatMap((p) => p.requirements.filter((r) => r.action && impact(r) > 0).map((r) => ({ ...r, company: p.company })))
    .sort((a, b) => impact(b) - impact(a) || (a.due ?? "").localeCompare(b.due ?? ""));

const nextDeadline = [...postings].sort((a, b) => a.deadline.localeCompare(b.deadline))[0];
const nextInterview = postings.find((p) => p.interviewAt);
const strength = Math.round((demoUser.profile.confirmed / demoUser.profile.total) * 100);

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

/** One segment per requirement: the posting's gap matrix at a glance. Wider segments are required ones. */
function CoverageStrip({ posting }: { posting: Posting }) {
    return (
        <div className="flex h-2 w-full gap-0.5" aria-hidden="true">
            {posting.requirements.map((r) => (
                <span key={r.text} className={cx("h-full rounded-full", coverageColor[r.coverage], r.importance === "required" ? "flex-2" : "flex-1")} />
            ))}
        </div>
    );
}

export function DashboardPage() {
    const { user } = useAuth();
    const firstName = user?.displayName.split(/\s+/)[0] ?? "";
    const top = actions[0];

    return (
        <div className="mx-auto flex max-w-6xl flex-col gap-6 px-4 py-8 md:px-8">
            <header className="flex flex-wrap items-end justify-between gap-4">
                <div className="flex flex-col gap-1">
                    <h1 className="text-display-xs font-semibold text-primary">Goeiemorgen, {firstName}</h1>
                    <p className="text-md text-tertiary">
                        {demoUser.school}. Stage from {fmt(demoUser.stage.start)} to {fmt(demoUser.stage.end)} 2027.
                    </p>
                </div>
                <BadgeWithDot color="warning" type="pill-color" size="md">
                    Demo data
                </BadgeWithDot>
            </header>

            {/* The one loud element: the single most valuable thing to do today. */}
            <section className="rounded-2xl bg-brand-section p-6 md:p-8">
                <p className="text-sm font-medium text-brand-tertiary_alt">Biggest gap you can close this week</p>
                <p className="mt-3 max-w-2xl text-display-xs font-semibold text-primary_on-brand md:text-display-sm">
                    {top.action} in <span className="font-mono">{top.project}</span>
                </p>
                <p className="mt-3 text-md text-tertiary_on-brand">
                    Covers "{top.text}" for {top.company}. Due {fmt(top.due!)}.
                </p>
            </section>

            <div className="grid gap-6 md:grid-cols-3">
                <Panel title="Profile strength">
                    <div className="flex flex-col gap-4 p-5">
                        <div className="flex items-baseline gap-2">
                            <span className="text-display-md font-semibold text-primary">{strength}%</span>
                            <span className="text-sm text-tertiary">
                                {demoUser.profile.confirmed} of {demoUser.profile.total} items confirmed
                            </span>
                        </div>
                        <div className="h-2 rounded-full bg-quaternary">
                            <div className="h-full rounded-full bg-brand-solid" style={{ width: `${strength}%` }} />
                        </div>
                        <Button color="link-color" size="sm" href="/profile">
                            Review 3 items
                        </Button>
                    </div>
                </Panel>

                <Panel title="Sources">
                    <ul className="flex flex-col divide-y divide-secondary">
                        {demoUser.sources.map((s) => {
                            const Icon = s.kind === "GitHub" ? GitBranch01 : s.kind === "CV" ? FileCheck02 : Globe01;
                            return (
                                <li key={s.kind} className="flex items-center gap-3 px-5 py-3">
                                    <Icon className="size-5 shrink-0 text-fg-quaternary" aria-hidden="true" />
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
                                <span className="w-14 shrink-0 text-display-xs font-semibold text-primary tabular-nums">{score(p.requirements)}%</span>
                                <div className="flex min-w-0 flex-1 flex-col gap-2">
                                    <div className="flex flex-wrap items-center gap-2">
                                        <p className="truncate text-sm font-semibold text-primary">{p.title}</p>
                                        <Badge color="gray" type="pill-color" size="sm">
                                            {p.language}
                                        </Badge>
                                    </div>
                                    <p className="text-sm text-tertiary">
                                        {p.company}, {p.city}. Apply by {fmt(p.deadline)}
                                    </p>
                                    <CoverageStrip posting={p} />
                                </div>
                            </li>
                        ))}
                    </ul>
                    <p className="flex flex-wrap gap-x-4 gap-y-1 border-t border-secondary px-5 py-3 text-xs text-tertiary">
                        <span className="flex items-center gap-1.5"><span className="size-2 rounded-full bg-success-solid" /> Covered</span>
                        <span className="flex items-center gap-1.5"><span className="size-2 rounded-full bg-warning-solid" /> Partly covered</span>
                        <span className="flex items-center gap-1.5"><span className="size-2 rounded-full bg-quaternary" /> Missing</span>
                        <span>Score: required counts twice, nice-to-have once.</span>
                    </p>
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

            <Panel title={`Match score over time, ${postings[0].company}`}>
                <div className="h-60 px-2 py-4">
                    <ResponsiveContainer width="100%" height="100%">
                        <AreaChart data={trend} margin={{ left: 0, right: 16, top: 8, bottom: 0 }}>
                            <defs>
                                <linearGradient id="trendFill" x1="0" y1="0" x2="0" y2="1">
                                    <stop offset="0%" stopColor="currentColor" stopOpacity={0.25} />
                                    <stop offset="100%" stopColor="currentColor" stopOpacity={0} />
                                </linearGradient>
                            </defs>
                            <CartesianGrid vertical={false} stroke="var(--color-border-secondary)" />
                            <XAxis dataKey="date" axisLine={false} tickLine={false} tick={{ fontSize: 12 }} className="text-xs text-tertiary" />
                            <YAxis domain={[0, 100]} unit="%" axisLine={false} tickLine={false} width={44} tick={{ fontSize: 12 }} className="text-xs text-tertiary" />
                            <Tooltip content={<ChartTooltipContent />} formatter={(v) => `${v}%`} cursor={{ className: "stroke-utility-brand-600 stroke-2" }} />
                            <Area dataKey="score" name="Score" type="monotone" stroke="currentColor" strokeWidth={2} fill="url(#trendFill)" className="text-utility-brand-600" />
                        </AreaChart>
                    </ResponsiveContainer>
                </div>
            </Panel>
        </div>
    );
}
