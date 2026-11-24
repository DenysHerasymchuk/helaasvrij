import { useState } from "react";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { TooltipContentProps } from "recharts";
import type { NameType, ValueType } from "recharts/types/component/DefaultTooltipContent";
import { cx } from "@/utils/cx";
import { reduceMotion } from "./motion";

const tick = { fontSize: 12, fill: "var(--color-text-tertiary)" };

function TooltipCard({ children }: { children: React.ReactNode }) {
    return <div className="min-w-36 rounded-lg bg-primary px-3 py-2 text-xs shadow-lg ring-1 ring-secondary">{children}</div>;
}

type MonthRow = { month: string; applied: number; interview: number; offer: number };
type SeriesKey = "applied" | "interview" | "offer";

const series: { key: SeriesKey; label: string; color: string }[] = [
    { key: "applied", label: "Applied", color: "var(--chart-applied)" },
    { key: "interview", label: "Interview", color: "var(--chart-interview)" },
    { key: "offer", label: "Offer", color: "var(--chart-offer)" },
];

const pct = (part: number, whole: number) => (whole ? Math.round((part / whole) * 100) : 0);

export function ApplicationsBarChart({ data }: { data: MonthRow[] }) {
    const [months, setMonths] = useState<3 | 6>(6);
    const [hidden, setHidden] = useState<Set<SeriesKey>>(new Set());
    const rows = data.slice(-months);
    const sum = (k: SeriesKey) => rows.reduce((s, r) => s + r[k], 0);
    const applied = sum("applied");

    const toggle = (key: SeriesKey) =>
        setHidden((h) => {
            const next = new Set(h);
            if (next.has(key)) next.delete(key);
            else next.add(key);
            return next;
        });

    return (
        <div className="flex flex-col gap-4 p-5">
            <div className="flex flex-wrap items-end justify-between gap-4">
                <dl className="flex gap-8">
                    <div>
                        <dt className="text-sm text-tertiary">Interview rate</dt>
                        <dd className="text-display-xs font-semibold text-primary tabular-nums">{pct(sum("interview"), applied)}%</dd>
                        <dd className="text-xs text-tertiary tabular-nums">
                            {sum("interview")} of {applied} applications
                        </dd>
                    </div>
                    <div>
                        <dt className="text-sm text-tertiary">Offer rate</dt>
                        <dd className="text-display-xs font-semibold text-primary tabular-nums">{pct(sum("offer"), applied)}%</dd>
                        <dd className="text-xs text-tertiary tabular-nums">
                            {sum("offer")} of {applied} applications
                        </dd>
                    </div>
                    <div>
                        <dt className="text-sm text-tertiary">Applications</dt>
                        <dd className="text-display-xs font-semibold text-primary tabular-nums">{applied}</dd>
                        <dd className="text-xs text-tertiary">sent</dd>
                    </div>
                </dl>
                <div className="flex rounded-lg p-0.5 ring-1 ring-secondary" role="group" aria-label="Time range">
                    {([3, 6] as const).map((m) => (
                        <button
                            key={m}
                            type="button"
                            aria-pressed={months === m}
                            onClick={() => setMonths(m)}
                            className={cx(
                                "cursor-pointer rounded-md px-3 py-1 text-sm font-semibold outline-focus-ring focus-visible:outline-2",
                                months === m ? "bg-secondary text-primary" : "text-tertiary hover:text-secondary",
                            )}
                        >
                            {m} months
                        </button>
                    ))}
                </div>
            </div>

            <div className="h-56" role="img" aria-label={`Applications per month, last ${months} months`}>
                <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={rows} barGap={2} barCategoryGap="28%" margin={{ left: 0, right: 8, top: 8, bottom: 0 }}>
                        <CartesianGrid vertical={false} stroke="var(--color-border-secondary)" />
                        <XAxis dataKey="month" axisLine={false} tickLine={false} tickMargin={10} tick={tick} />
                        <YAxis allowDecimals={false} axisLine={false} tickLine={false} width={28} tick={tick} />
                        <Tooltip content={BarTooltip} cursor={{ fill: "var(--color-bg-secondary)" }} />
                        {series.map((s) => (
                            <Bar
                                key={s.key}
                                dataKey={s.key}
                                name={s.label}
                                fill={s.color}
                                hide={hidden.has(s.key)}
                                maxBarSize={24}
                                radius={[4, 4, 0, 0]}
                                isAnimationActive={!reduceMotion}
                                animationDuration={600}
                            />
                        ))}
                    </BarChart>
                </ResponsiveContainer>
            </div>

            <div className="flex flex-wrap gap-2" role="group" aria-label="Show or hide series">
                {series.map((s) => (
                    <button
                        key={s.key}
                        type="button"
                        aria-pressed={!hidden.has(s.key)}
                        onClick={() => toggle(s.key)}
                        className={cx(
                            "flex cursor-pointer items-center gap-2 rounded-full px-3 py-1 text-sm ring-1 ring-secondary outline-focus-ring hover:bg-primary_hover focus-visible:outline-2",
                            hidden.has(s.key) && "opacity-50",
                        )}
                    >
                        <span className="size-2.5 rounded-full" style={{ background: s.color }} aria-hidden="true" />
                        <span className={cx("text-secondary", hidden.has(s.key) && "line-through")}>{s.label}</span>
                    </button>
                ))}
            </div>

            <table className="sr-only">
                <caption>Applications per month</caption>
                <thead>
                    <tr>
                        <th scope="col">Month</th>
                        {series.map((s) => (
                            <th key={s.key} scope="col">
                                {s.label}
                            </th>
                        ))}
                    </tr>
                </thead>
                <tbody>
                    {rows.map((r) => (
                        <tr key={r.month}>
                            <th scope="row">{r.month}</th>
                            <td>{r.applied}</td>
                            <td>{r.interview}</td>
                            <td>{r.offer}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}

function BarTooltip({ active, payload, label }: TooltipContentProps<ValueType, NameType>) {
    if (!active || !payload?.length) return null;
    const row = payload[0].payload as MonthRow;
    return (
        <TooltipCard>
            <p className="mb-1 font-semibold text-primary">{label}</p>
            {payload.map((p) => (
                <p key={String(p.dataKey)} className="flex items-center gap-2">
                    <span className="size-2 rounded-full" style={{ background: p.color }} aria-hidden="true" />
                    <span className="flex-1 text-tertiary">{p.name}</span>
                    <span className="font-semibold text-primary tabular-nums">{p.value}</span>
                </p>
            ))}
            <p className="mt-1 border-t border-secondary pt-1 text-tertiary">Interview rate {pct(row.interview, row.applied)}%</p>
        </TooltipCard>
    );
}
