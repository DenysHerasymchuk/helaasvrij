import { type KeyboardEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router";
import { AlarmClock, CalendarPlus01, CheckSquare, ChevronLeft, ChevronRight, Flag01, MessageChatCircle } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { cx } from "@/utils/cx";
import { addDays, downloadInterviewIcs, formatBrusselsTime, todayInBrussels } from "./brussels";

type EventType = "interview" | "deadline" | "gapDue" | "stageStart" | "stageEnd";

type CalendarEvent = { type: EventType; date: string; at: string | null; title: string; postingId: string; link: string };

// Colour is never alone: every type also has its own icon and label.
const kinds: Record<EventType, { label: string; icon: typeof AlarmClock; dot: string; chip: string; icon_: string }> = {
    interview: { label: "Interview", icon: MessageChatCircle, dot: "bg-utility-yellow-500", chip: "bg-utility-yellow-50 text-utility-yellow-700 ring-utility-yellow-200", icon_: "text-utility-yellow-700" },
    deadline: { label: "Deadline", icon: AlarmClock, dot: "bg-utility-red-500", chip: "bg-utility-red-50 text-utility-red-700 ring-utility-red-200", icon_: "text-utility-red-700" },
    gapDue: { label: "Gap action due", icon: CheckSquare, dot: "bg-utility-blue-500", chip: "bg-utility-blue-50 text-utility-blue-700 ring-utility-blue-200", icon_: "text-utility-blue-700" },
    stageStart: { label: "Internship starts", icon: Flag01, dot: "bg-utility-green-500", chip: "bg-utility-green-50 text-utility-green-700 ring-utility-green-200", icon_: "text-utility-green-700" },
    stageEnd: { label: "Internship ends", icon: Flag01, dot: "bg-utility-green-500", chip: "bg-utility-green-50 text-utility-green-700 ring-utility-green-200", icon_: "text-utility-green-700" },
};

const legend: EventType[] = ["interview", "deadline", "gapDue", "stageStart"];
const weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
const UPCOMING_DAYS = 14;

const monthFmt = new Intl.DateTimeFormat("en-BE", { month: "long", year: "numeric", timeZone: "UTC" });
const dayFmt = new Intl.DateTimeFormat("en-BE", { weekday: "long", day: "numeric", month: "long", timeZone: "UTC" });
const shortDayFmt = new Intl.DateTimeFormat("en-BE", { weekday: "short", day: "numeric", month: "short", timeZone: "UTC" });
const utc = (date: string) => new Date(`${date}T00:00:00Z`);

/** The 42 days (six Monday-first weeks) shown for a "yyyy-MM" month. */
function gridDays(month: string): string[] {
    const first = `${month}-01`;
    const start = addDays(first, -((utc(first).getUTCDay() + 6) % 7));
    return Array.from({ length: 42 }, (_, i) => addDays(start, i));
}

const shiftMonth = (month: string, by: number) => {
    const d = utc(`${month}-01`);
    d.setUTCMonth(d.getUTCMonth() + by);
    return d.toISOString().slice(0, 7);
};

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

export function CalendarPage() {
    const today = todayInBrussels();
    const [month, setMonth] = useState(today.slice(0, 7));
    const [selected, setSelected] = useState(today);
    const days = useMemo(() => gridDays(month), [month]);
    const grid = useEvents(days[0], days[41]);
    const upcoming = useEvents(today, addDays(today, UPCOMING_DAYS - 1));

    const byDay = useMemo(() => {
        const map = new Map<string, CalendarEvent[]>();
        for (const e of grid.events ?? []) map.set(e.date, [...(map.get(e.date) ?? []), e]);
        return map;
    }, [grid.events]);

    function select(date: string) {
        setSelected(date);
        if (date.slice(0, 7) !== month) setMonth(date.slice(0, 7));
    }

    function goToMonth(by: number) {
        const next = shiftMonth(month, by);
        setMonth(next);
        setSelected(next === today.slice(0, 7) ? today : `${next}-01`);
    }

    return (
        <div className="mx-auto flex max-w-[1400px] flex-col gap-6 px-4 py-8 md:px-8">
            <div className="flex flex-wrap items-end justify-between gap-4">
                <div>
                    <h1 className="text-display-xs font-semibold text-primary">Calendar</h1>
                    <p className="mt-1 text-md text-tertiary">Interviews, deadlines and gap actions from your postings, in Belgian time.</p>
                </div>
            </div>

            <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
                <div className="order-2 flex min-w-0 flex-col gap-4 lg:order-1">
                    <section aria-labelledby="month-title" className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
                        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-secondary px-4 py-3 md:px-5">
                            <h2 id="month-title" className="text-lg font-semibold text-primary" aria-live="polite">
                                {monthFmt.format(utc(`${month}-01`))}
                            </h2>
                            <div className="flex items-center gap-2">
                                <Button size="sm" color="secondary" iconLeading={ChevronLeft} aria-label="Previous month" onPress={() => goToMonth(-1)} />
                                <Button size="sm" color="secondary" onPress={() => select(today)} isDisabled={selected === today}>
                                    Today
                                </Button>
                                <Button size="sm" color="secondary" iconLeading={ChevronRight} aria-label="Next month" onPress={() => goToMonth(1)} />
                            </div>
                        </header>

                        {grid.error ? (
                            <div className="flex flex-col items-start gap-3 p-5">
                                <p role="alert" className="text-md text-error-primary">
                                    {grid.error}
                                </p>
                                <Button size="sm" color="secondary" onPress={grid.load}>
                                    Try again
                                </Button>
                            </div>
                        ) : (
                            <MonthGrid days={days} month={month} today={today} selected={selected} byDay={byDay} loading={grid.events === null} onSelect={select} />
                        )}

                        <ul aria-label="Legend" className="flex flex-wrap gap-x-4 gap-y-2 border-t border-secondary px-4 py-3 md:px-5">
                            {legend.map((type) => {
                                const k = kinds[type];
                                return (
                                    <li key={type} className="flex items-center gap-1.5 text-sm text-tertiary">
                                        <span className={cx("size-2 rounded-full", k.dot)} aria-hidden="true" />
                                        <k.icon className={cx("size-4", k.icon_)} aria-hidden="true" />
                                        {type === "stageStart" ? "Internship starts or ends" : k.label}
                                    </li>
                                );
                            })}
                        </ul>
                    </section>

                    <section aria-labelledby="day-title" className="rounded-xl bg-primary p-4 shadow-xs ring-1 ring-secondary md:p-5">
                        <h2 id="day-title" className="text-md font-semibold text-primary">
                            {selected === today ? "Today, " : ""}
                            {dayFmt.format(utc(selected))}
                        </h2>
                        {grid.events === null ? (
                            <p className="mt-2 text-sm text-tertiary">Loading...</p>
                        ) : (byDay.get(selected) ?? []).length === 0 ? (
                            <p className="mt-2 text-sm text-tertiary">Nothing on this day.</p>
                        ) : (
                            <EventList events={byDay.get(selected)!} />
                        )}
                    </section>
                </div>

                <aside aria-labelledby="upcoming-title" className="order-1 self-start rounded-xl bg-primary p-4 shadow-xs ring-1 ring-secondary md:p-5 lg:order-2">
                    <h2 id="upcoming-title" className="text-md font-semibold text-primary">
                        Upcoming
                    </h2>
                    <p className="text-sm text-tertiary">The next {UPCOMING_DAYS} days</p>
                    {upcoming.error ? (
                        <div className="mt-3 flex flex-col items-start gap-3">
                            <p role="alert" className="text-sm text-error-primary">
                                {upcoming.error}
                            </p>
                            <Button size="sm" color="secondary" onPress={upcoming.load}>
                                Try again
                            </Button>
                        </div>
                    ) : upcoming.events === null ? (
                        <p role="status" className="mt-3 text-sm text-tertiary">
                            Loading...
                        </p>
                    ) : (
                        <>
                            {!upcoming.events.some((e) => e.type === "interview") && (
                                <div className="mt-3 flex flex-col items-start gap-2 rounded-lg bg-secondary p-3">
                                    <p className="text-sm text-secondary">No interviews yet. When you move an application to Interview, add the date here.</p>
                                    <Button size="sm" color="secondary" href="/applications">
                                        Open applications
                                    </Button>
                                </div>
                            )}
                            {upcoming.events.length > 0 && <UpcomingList events={upcoming.events} today={today} onPick={select} />}
                        </>
                    )}
                </aside>
            </div>
        </div>
    );
}

type GridProps = {
    days: string[];
    month: string;
    today: string;
    selected: string;
    byDay: Map<string, CalendarEvent[]>;
    loading: boolean;
    onSelect: (date: string) => void;
};

/** Month grid with one tab stop: Tab lands on the selected day, arrows move, Enter or Space picks. */
function MonthGrid({ days, month, today, selected, byDay, loading, onSelect }: GridProps) {
    const ref = useRef<HTMLDivElement>(null);
    const keyMoved = useRef(false);

    useEffect(() => {
        if (!keyMoved.current) return;
        keyMoved.current = false;
        ref.current?.querySelector<HTMLButtonElement>(`[data-date="${selected}"]`)?.focus();
    }, [selected, month]);

    function onKeyDown(e: KeyboardEvent<HTMLButtonElement>, date: string) {
        const step = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 }[e.key];
        if (step === undefined) return;
        e.preventDefault();
        keyMoved.current = true;
        onSelect(addDays(date, step));
    }

    const weeks = Array.from({ length: 6 }, (_, w) => days.slice(w * 7, w * 7 + 7));

    return (
        <div ref={ref} role="grid" aria-labelledby="month-title" aria-busy={loading} className="px-2 pb-2 md:px-3">
            <div role="row" className="grid grid-cols-7">
                {weekdays.map((d) => (
                    <div key={d} role="columnheader" className="py-2 text-center text-xs font-medium text-quaternary">
                        {d}
                    </div>
                ))}
            </div>
            {weeks.map((week) => (
                <div key={week[0]} role="row" className="grid grid-cols-7 gap-1">
                    {week.map((date) => {
                        const events = byDay.get(date) ?? [];
                        const inMonth = date.slice(0, 7) === month;
                        const isToday = date === today;
                        const isSelected = date === selected;
                        const label = `${dayFmt.format(utc(date))}${isToday ? ", today" : ""}, ${events.length === 0 ? "no events" : events.length === 1 ? "1 event" : `${events.length} events`}`;
                        return (
                            <div key={date} role="gridcell" aria-selected={isSelected}>
                                <button
                                    type="button"
                                    data-date={date}
                                    tabIndex={isSelected ? 0 : -1}
                                    aria-label={label}
                                    onClick={() => onSelect(date)}
                                    onKeyDown={(e) => onKeyDown(e, date)}
                                    className={cx(
                                        "flex h-14 w-full flex-col items-center gap-1 rounded-lg p-1 text-left outline-focus-ring transition-colors focus-visible:outline-2 focus-visible:outline-offset-1 md:h-24 md:items-stretch md:p-1.5",
                                        isSelected ? "bg-secondary ring-2 ring-brand ring-inset" : "hover:bg-primary_hover",
                                    )}
                                >
                                    <span
                                        className={cx(
                                            "flex size-7 shrink-0 items-center justify-center rounded-full text-sm tabular-nums",
                                            isToday ? "bg-brand-solid font-semibold text-white" : inMonth ? "text-secondary" : "text-quaternary",
                                        )}
                                    >
                                        {Number(date.slice(8))}
                                    </span>
                                    {/* Phone: dots. Wider: short labels. */}
                                    <span className="flex flex-wrap justify-center gap-0.5 md:hidden" aria-hidden="true">
                                        {events.slice(0, 3).map((e, i) => (
                                            <span key={i} className={cx("size-1.5 rounded-full", kinds[e.type].dot)} />
                                        ))}
                                    </span>
                                    <span className="hidden min-w-0 flex-col gap-0.5 md:flex" aria-hidden="true">
                                        {events.slice(0, 2).map((e, i) => {
                                            const k = kinds[e.type];
                                            return (
                                                <span key={i} className={cx("flex min-w-0 items-center gap-1 rounded px-1 py-0.5 text-xs ring-1 ring-inset", k.chip, !inMonth && "opacity-60")}>
                                                    <k.icon className="size-3 shrink-0" />
                                                    <span className="truncate">{e.type === "interview" ? e.title.replace(/^Interview at /, "") : e.title}</span>
                                                </span>
                                            );
                                        })}
                                        {events.length > 2 && <span className="px-1 text-xs text-tertiary">+{events.length - 2} more</span>}
                                    </span>
                                </button>
                            </div>
                        );
                    })}
                </div>
            ))}
        </div>
    );
}

function EventList({ events }: { events: CalendarEvent[] }) {
    return (
        <ul className="mt-3 flex flex-col gap-2">
            {events.map((e, i) => (
                <EventRow key={`${e.type}-${e.postingId}-${i}`} event={e} />
            ))}
        </ul>
    );
}

function EventRow({ event }: { event: CalendarEvent }) {
    const k = kinds[event.type];
    return (
        <li className="flex items-start gap-3 rounded-lg p-2 ring-1 ring-secondary">
            <span className={cx("mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-lg ring-1 ring-inset", k.chip)}>
                <k.icon className="size-4" aria-hidden="true" />
            </span>
            <div className="min-w-0 flex-1">
                <Link to={event.link} className="block text-sm font-semibold break-words text-primary outline-focus-ring hover:underline focus-visible:outline-2">
                    {event.title}
                </Link>
                <p className="text-xs text-tertiary">
                    {k.label}
                    {event.at && ` at ${formatBrusselsTime(event.at)}`}
                </p>
                {event.type === "interview" && event.at && (
                    <Button
                        color="link-color"
                        size="sm"
                        iconLeading={CalendarPlus01}
                        className="mt-1"
                        onPress={() => downloadInterviewIcs({ id: event.postingId, title: event.title, at: event.at!, postingId: event.postingId })}
                    >
                        Add to my calendar
                    </Button>
                )}
            </div>
        </li>
    );
}

function UpcomingList({ events, today, onPick }: { events: CalendarEvent[]; today: string; onPick: (date: string) => void }) {
    const groups = new Map<string, CalendarEvent[]>();
    for (const e of events) groups.set(e.date, [...(groups.get(e.date) ?? []), e]);
    const tomorrow = addDays(today, 1);

    return (
        <ol className="mt-3 flex flex-col gap-4">
            {[...groups].map(([date, list]) => (
                <li key={date}>
                    <button
                        type="button"
                        onClick={() => onPick(date)}
                        className="rounded-xs text-sm font-medium text-secondary outline-focus-ring hover:underline focus-visible:outline-2"
                        aria-label={`Show ${dayFmt.format(utc(date))} in the month`}
                    >
                        {date === today ? "Today" : date === tomorrow ? "Tomorrow" : shortDayFmt.format(utc(date))}
                    </button>
                    <ul className="mt-2 flex flex-col gap-2">
                        {list.map((e, i) => (
                            <EventRow key={`${e.type}-${e.postingId}-${i}`} event={e} />
                        ))}
                    </ul>
                </li>
            ))}
        </ol>
    );
}
