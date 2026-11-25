import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router";
import { AlarmClock, CheckSquare, ChevronLeft, ChevronRight, MessageChatCircle } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { cx } from "@/utils/cx";
import { addDays, formatBrusselsTime, todayInBrussels } from "./brussels";

type EventType = "interview" | "deadline" | "gapDue";

type CalendarEvent = { type: EventType; date: string; at: string | null; title: string; postingId: string; link: string };

// Colour is never alone: every type also has its own icon and label.
const kinds: Record<EventType, { label: string; icon: typeof AlarmClock; dot: string; chip: string; icon_: string }> = {
    interview: { label: "Interview", icon: MessageChatCircle, dot: "bg-utility-yellow-500", chip: "bg-utility-yellow-50 text-utility-yellow-700 ring-utility-yellow-200", icon_: "text-utility-yellow-700" },
    deadline: { label: "Deadline", icon: AlarmClock, dot: "bg-utility-red-500", chip: "bg-utility-red-50 text-utility-red-700 ring-utility-red-200", icon_: "text-utility-red-700" },
    gapDue: { label: "Gap action due", icon: CheckSquare, dot: "bg-utility-blue-500", chip: "bg-utility-blue-50 text-utility-blue-700 ring-utility-blue-200", icon_: "text-utility-blue-700" },
};

const legend: EventType[] = ["interview", "deadline", "gapDue"];

const monthFmt = new Intl.DateTimeFormat("en-BE", { month: "long", year: "numeric", timeZone: "UTC" });
const weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
const shiftMonth = (date: string, by: number) => {
    const d = utc(`${date.slice(0, 7)}-01`);
    d.setUTCMonth(d.getUTCMonth() + by);
    return d.toISOString().slice(0, 10);
};
const dayFmt = new Intl.DateTimeFormat("en-BE", { weekday: "long", day: "numeric", month: "long", timeZone: "UTC" });
const utc = (date: string) => new Date(`${date}T00:00:00Z`);

/** The 42 days (six Monday-first weeks) shown for a "yyyy-MM" month. */
function gridDays(month: string): string[] {
    const first = `${month}-01`;
    const start = addDays(first, -((utc(first).getUTCDay() + 6) % 7));
    return Array.from({ length: 42 }, (_, i) => addDays(start, i));
}


const message = (e: unknown) => (e instanceof ApiError ? e.message : "Something went wrong. Try again.");

function useEvents(from: string, to: string) {
    const [events, setEvents] = useState<CalendarEvent[] | null>(null);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setError(null);
        setEvents(null);
        try {
            setEvents((await api.get<CalendarEvent[]>(`/api/calendar?from=${from}&to=${to}`)) ?? []);
        } catch (e) {
            setError(message(e));
        }
    }, [from, to]);

    useEffect(() => {
        load();
    }, [load]);

    return { events, error, load };
}


function Chip({ e }: { e: CalendarEvent }) {
    const k = kinds[e.type];
    return (
        <Link
            to={e.link}
            title={`${k.label}: ${e.title}`}
            className={cx("flex items-center gap-1 rounded-md px-1.5 py-1 text-xs font-medium ring-1 ring-inset hover:opacity-80", k.chip)}
        >
            <k.icon className="size-3.5 shrink-0" aria-hidden="true" />
            <span className="truncate">
                {e.at ? `${formatBrusselsTime(e.at)} ` : ""}
                {e.title}
            </span>
        </Link>
    );
}

export function CalendarPage() {
    const today = todayInBrussels();
    const [selected, setSelected] = useState(today);
    const [view, setView] = useState<"month" | "week">("month");
    const month = selected.slice(0, 7);
    const days = useMemo(() => gridDays(month), [month]);
    const weekStart = addDays(selected, -((utc(selected).getUTCDay() + 6) % 7));
    const shown = view === "week" ? Array.from({ length: 7 }, (_, i) => addDays(weekStart, i)) : days;
    const grid = useEvents(days[0], days[41]);

    const byDay = useMemo(() => {
        const map = new Map<string, CalendarEvent[]>();
        for (const e of grid.events ?? []) map.set(e.date, [...(map.get(e.date) ?? []), e]);
        return map;
    }, [grid.events]);

    const step = (by: number) => setSelected(view === "week" ? addDays(selected, by * 7) : shiftMonth(selected, by));
    const title = view === "week" ? `${dayFmt.format(utc(shown[0]))} to ${dayFmt.format(utc(shown[6]))}` : monthFmt.format(utc(`${month}-01`));
    const dayEvents = byDay.get(selected) ?? [];

    return (
        <div className="mx-auto flex h-[calc(100dvh-1rem)] max-w-[1600px] flex-col gap-3 px-4 py-4 md:px-6">
            <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="flex items-center gap-3">
                    <h1 className="text-display-xs font-semibold text-primary">Calendar</h1>
                    <h2 className="text-lg text-tertiary" aria-live="polite">
                        {title}
                    </h2>
                </div>
                <div className="flex items-center gap-2">
                    <Button size="sm" color={view === "month" ? "primary" : "secondary"} onPress={() => setView("month")}>
                        Month
                    </Button>
                    <Button size="sm" color={view === "week" ? "primary" : "secondary"} onPress={() => setView("week")}>
                        Week
                    </Button>
                    <Button size="sm" color="secondary" iconLeading={ChevronLeft} aria-label="Previous" onPress={() => step(-1)} />
                    <Button size="sm" color="secondary" onPress={() => setSelected(today)}>
                        Today
                    </Button>
                    <Button size="sm" color="secondary" iconLeading={ChevronRight} aria-label="Next" onPress={() => step(1)} />
                </div>
            </div>

            <div className="grid min-h-0 flex-1 gap-4 lg:grid-cols-[minmax(0,1fr)_320px]">
                {grid.error ? (
                    <div className="flex flex-col items-start gap-3">
                        <p role="alert" className="text-md text-error-primary">
                            {grid.error}
                        </p>
                        <Button size="sm" color="secondary" onPress={grid.load}>
                            Try again
                        </Button>
                    </div>
                ) : (
                    <div className="min-h-0 overflow-auto rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
                        <div className={cx("grid h-full min-w-[720px] grid-cols-7", view === "month" ? "grid-rows-[auto_repeat(6,minmax(0,1fr))]" : "grid-rows-[auto_minmax(0,1fr)]")}>
                            {weekdays.map((d) => (
                                <div key={d} className="border-b border-secondary px-3 py-2 text-sm font-medium text-tertiary">
                                    {d}
                                </div>
                            ))}
                            {shown.map((date, i) => {
                                const inMonth = date.slice(0, 7) === month;
                                const events = byDay.get(date) ?? [];
                                const max = view === "week" ? events.length : 2;
                                return (
                                    <div
                                        key={date}
                                        onClick={() => setSelected(date)}
                                        className={cx(
                                            "flex min-h-0 cursor-pointer flex-col gap-1 overflow-hidden border-secondary p-1.5 hover:bg-secondary/50",
                                            i % 7 !== 6 && "border-r",
                                            view === "month" && i < 35 && "border-b",
                                            view === "month" && !inMonth && "bg-secondary/40",
                                            date === selected && "bg-brand-primary/40",
                                        )}
                                    >
                                        <span
                                            className={cx(
                                                "flex size-6 shrink-0 items-center justify-center self-start rounded-full text-sm",
                                                date === today ? "bg-brand-solid font-semibold text-white" : inMonth ? "text-secondary" : "text-quaternary",
                                            )}
                                        >
                                            {Number(date.slice(8))}
                                        </span>
                                        {events.slice(0, max).map((e, n) => (
                                            <Chip key={`${e.type}-${e.postingId}-${n}`} e={e} />
                                        ))}
                                        {events.length > max && <span className="px-1 text-xs text-tertiary">+{events.length - max} more</span>}
                                    </div>
                                );
                            })}
                        </div>
                    </div>
                )}

                <aside aria-labelledby="day-title" className="min-h-0 overflow-auto rounded-xl bg-primary p-4 shadow-xs ring-1 ring-secondary">
                    <h2 id="day-title" className="text-md font-semibold text-primary">
                        {selected === today ? "Today, " : ""}
                        {dayFmt.format(utc(selected))}
                    </h2>
                    {grid.events === null ? (
                        <p role="status" className="mt-2 text-sm text-tertiary">
                            Loading...
                        </p>
                    ) : dayEvents.length === 0 ? (
                        <p className="mt-2 text-sm text-tertiary">Nothing on this day.</p>
                    ) : (
                        <ul className="mt-3 flex flex-col gap-2">
                            {dayEvents.map((e, n) => (
                                <li key={`${e.type}-${e.postingId}-${n}`} className="flex flex-col gap-1">
                                    <span className="text-xs text-tertiary">{kinds[e.type].label}</span>
                                    <Link to={e.link} className={cx("rounded-lg px-2.5 py-2 text-sm font-medium ring-1 ring-inset hover:opacity-80", kinds[e.type].chip)}>
                                        {e.at ? `${formatBrusselsTime(e.at)} ` : ""}
                                        {e.title}
                                    </Link>
                                </li>
                            ))}
                        </ul>
                    )}
                </aside>
            </div>

            <ul aria-label="Legend" className="flex flex-wrap gap-x-4 gap-y-1">
                {legend.map((type) => {
                    const k = kinds[type];
                    return (
                        <li key={type} className="flex items-center gap-1.5 text-sm text-tertiary">
                            <k.icon className={cx("size-4", k.icon_)} aria-hidden="true" />
                            {k.label}
                        </li>
                    );
                })}
            </ul>
        </div>
    );
}
