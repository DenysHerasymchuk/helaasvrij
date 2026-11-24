import { useState } from "react";
import { Bar, BarChart, CartesianGrid, Cell, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { TooltipContentProps } from "recharts";
import type { NameType, ValueType } from "recharts/types/component/DefaultTooltipContent";
import type { Coverage, Posting } from "@/features/demo/demo-user";
import { cx } from "@/utils/cx";

// Colours live in index.css as --chart-* (validated per theme). Missing has no data colour: it is the empty track.
export const coverageFill: Record<Coverage, string> = {
    strong: "var(--chart-coverage-strong)",
    partial: "var(--chart-coverage-partial)",
    none: "var(--color-bg-quaternary)",
};
const coverageLabel: Record<Coverage, string> = { strong: "Covered", partial: "Partly covered", none: "Missing" };
const surface = "var(--color-bg-primary)";
const tick = { fontSize: 12, fill: "var(--color-text-tertiary)" };

function TooltipCard({ children }: { children: React.ReactNode }) {
    return <div className="min-w-36 rounded-lg bg-primary px-3 py-2 text-xs shadow-lg ring-1 ring-secondary">{children}</div>;
}

/* ---------- Score ring: the match score with a circle that fills to it ---------- */

const ringColor = (pct: number) =>
    pct >= 75 ? "var(--color-fg-success-primary)" : pct >= 50 ? "var(--color-fg-warning-primary)" : "var(--color-fg-error-primary)";

export function ScoreRing({ value, size = 56 }: { value: number; size?: number }) {
    const stroke = 5;
    const r = (size - stroke) / 2;
    const c = 2 * Math.PI * r;
    return (
        <div className="relative shrink-0" style={{ width: size, height: size }} role="img" aria-label={`${value}% match`}>
            <svg width={size} height={size} className="-rotate-90" aria-hidden="true">
                <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="var(--color-bg-quaternary)" strokeWidth={stroke} />
                <circle
                    cx={size / 2}
                    cy={size / 2}
                    r={r}
                    fill="none"
                    stroke={ringColor(value)}
                    strokeWidth={stroke}
                    strokeLinecap="round"
                    strokeDasharray={`${(value / 100) * c} ${c}`}
                />
            </svg>
            <span className="absolute inset-0 flex items-center justify-center text-sm font-semibold text-primary tabular-nums">{value}%</span>
        </div>
    );
}

/* ---------- Coverage donut: hover to focus a slice, click to list its requirements ---------- */

export function CoverageDonut({ postings }: { postings: Posting[] }) {
    const [hovered, setHovered] = useState<Coverage | null>(null);
    const [selected, setSelected] = useState<Coverage | null>(null);

    const all = postings.flatMap((p) => p.requirements.map((r) => ({ ...r, company: p.company })));
    const total = all.length;
    const data = (["strong", "partial", "none"] as const).map((key) => {
        const value = all.filter((r) => r.coverage === key).length;
        return { key, name: coverageLabel[key], value, share: total ? Math.round((value / total) * 100) : 0 };
    });
    const focus = hovered ?? selected;
    const toggle = (key: Coverage) => setSelected((s) => (s === key ? null : key));

    return (
        <div className="flex flex-col">
            <div className="flex items-center gap-5 p-5">
                <div className="relative size-32 shrink-0" role="img" aria-label={`${data[0].value} of ${total} requirements covered`}>
                    <ResponsiveContainer width="100%" height="100%">
                        <PieChart>
                            <Pie
                                data={data}
                                dataKey="value"
                                nameKey="name"
                                innerRadius="72%"
                                outerRadius="100%"
                                startAngle={90}
                                endAngle={-270}
                                stroke={surface}
                                strokeWidth={2}
                                cornerRadius={4}
                                isAnimationActive={false}
                                className="cursor-pointer outline-none"
                                onMouseEnter={(_, i) => setHovered(data[i].key)}
                                onMouseLeave={() => setHovered(null)}
                                onClick={(_, i) => toggle(data[i].key)}
                            >
                                {data.map((d) => (
                                    <Cell key={d.key} fill={coverageFill[d.key]} fillOpacity={focus && focus !== d.key ? 0.3 : 1} />
                                ))}
                            </Pie>
                        </PieChart>
                    </ResponsiveContainer>
                    <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
                        {focus ? (
                            <>
                                <span className="text-display-xs font-semibold text-primary tabular-nums">{data.find((d) => d.key === focus)!.share}%</span>
                                <span className="text-xs text-tertiary">{coverageLabel[focus].toLowerCase()}</span>
                            </>
                        ) : (
                            <>
                                <span className="text-display-xs font-semibold text-primary tabular-nums">
                                    {data[0].value}/{total}
                                </span>
                                <span className="text-xs text-tertiary">covered</span>
                            </>
                        )}
                    </div>
                </div>
                <ul className="flex min-w-0 flex-1 flex-col gap-1">
                    {data.map((d) => (
                        <li key={d.key}>
                            <button
                                type="button"
                                aria-pressed={selected === d.key}
                                onClick={() => toggle(d.key)}
                                onMouseEnter={() => setHovered(d.key)}
                                onMouseLeave={() => setHovered(null)}
                                className={cx(
                                    "flex w-full cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm outline-focus-ring hover:bg-primary_hover focus-visible:outline-2",
                                    selected === d.key && "bg-secondary",
                                )}
                            >
                                <span className="size-2.5 shrink-0 rounded-full" style={{ background: coverageFill[d.key] }} aria-hidden="true" />
                                <span className="flex-1 text-left text-secondary">{d.name}</span>
                                <span className="font-semibold text-primary tabular-nums">{d.value}</span>
                                <span className="w-9 text-right text-tertiary tabular-nums">{d.share}%</span>
                            </button>
                        </li>
                    ))}
                </ul>
            </div>
            {selected && (
                <ul className="flex flex-col divide-y divide-secondary border-t border-secondary" aria-label={`${coverageLabel[selected]} requirements`}>
                    {all
                        .filter((r) => r.coverage === selected)
                        .map((r) => (
                            <li key={r.company + r.text} className="flex justify-between gap-3 px-5 py-2.5 text-sm">
                                <span className="text-primary">{r.text}</span>
                                <span className="shrink-0 text-tertiary">{r.company}</span>
                            </li>
                        ))}
                </ul>
            )}
        </div>
    );
}

/* ---------- Applications per month: grouped bars, toggle series and range ---------- */

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
                    </div>
                    <div>
                        <dt className="text-sm text-tertiary">Offer rate</dt>
                        <dd className="text-display-xs font-semibold text-primary tabular-nums">{pct(sum("offer"), applied)}%</dd>
                    </div>
                    <div>
                        <dt className="text-sm text-tertiary">Applications</dt>
                        <dd className="text-display-xs font-semibold text-primary tabular-nums">{applied}</dd>
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
                                isAnimationActive={false}
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
