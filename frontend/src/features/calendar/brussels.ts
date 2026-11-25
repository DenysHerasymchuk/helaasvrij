// Dates in Belgian time, whatever time zone the browser is in.
const ZONE = "Europe/Brussels";

const partsFmt = new Intl.DateTimeFormat("en-GB", {
    timeZone: ZONE,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
});

/** An instant as Belgian "yyyy-MM-dd" and "HH:mm". */
export function brusselsParts(instant: Date | string): { date: string; time: string } {
    const p = Object.fromEntries(partsFmt.formatToParts(new Date(instant)).map((x) => [x.type, x.value]));
    return { date: `${p.year}-${p.month}-${p.day}`, time: `${p.hour}:${p.minute}` };
}

/** Belgian wall-clock "yyyy-MM-dd" + "HH:mm" to an ISO instant (CET/CEST handled by Intl). */
export function brusselsToIso(date: string, time: string): string {
    const wall = Date.parse(`${date}T${time}:00Z`);
    // Offset = what Brussels shows for an instant minus that instant; apply twice so DST edges settle.
    let instant = wall;
    for (let i = 0; i < 2; i++) {
        const shown = brusselsParts(new Date(instant));
        instant = wall - (Date.parse(`${shown.date}T${shown.time}:00Z`) - instant);
    }
    return new Date(instant).toISOString();
}

export const todayInBrussels = () => brusselsParts(new Date()).date;

const dateTimeFmt = new Intl.DateTimeFormat("en-BE", { timeZone: ZONE, weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
const timeFmt = new Intl.DateTimeFormat("en-BE", { timeZone: ZONE, hour: "2-digit", minute: "2-digit" });

/** "Thu 3 Dec, 14:00" in Belgian time. */
export const formatBrussels = (iso: string) => dateTimeFmt.format(new Date(iso));
export const formatBrusselsTime = (iso: string) => timeFmt.format(new Date(iso));

/** "yyyy-MM-dd" plus n days, timezone-free. */
export function addDays(date: string, days: number): string {
    const d = new Date(`${date}T00:00:00Z`);
    d.setUTCDate(d.getUTCDate() + days);
    return d.toISOString().slice(0, 10);
}

// Standard Europe/Brussels rules (last Sunday of March / October), so calendar apps show the right hour.
const VTIMEZONE = [
    "BEGIN:VTIMEZONE",
    "TZID:Europe/Brussels",
    "BEGIN:DAYLIGHT",
    "TZOFFSETFROM:+0100",
    "TZOFFSETTO:+0200",
    "TZNAME:CEST",
    "DTSTART:19700329T020000",
    "RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1SU",
    "END:DAYLIGHT",
    "BEGIN:STANDARD",
    "TZOFFSETFROM:+0200",
    "TZOFFSETTO:+0100",
    "TZNAME:CET",
    "DTSTART:19701025T030000",
    "RRULE:FREQ=YEARLY;BYMONTH=10;BYDAY=-1SU",
    "END:STANDARD",
    "END:VTIMEZONE",
];

const escapeText = (s: string) => s.replace(/[\\;,]/g, (c) => `\\${c}`).replace(/\r?\n/g, "\\n");
const icsLocal = (iso: string) => {
    const { date, time } = brusselsParts(iso);
    return `${date.replaceAll("-", "")}T${time.replace(":", "")}00`;
};

/** One-hour interview as an .ics file (Europe/Brussels times). */
export function interviewIcs(event: { id: string; title: string; at: string; url: string }): string {
    const end = new Date(Date.parse(event.at) + 60 * 60 * 1000).toISOString();
    const stamp = new Date().toISOString().replace(/[-:]/g, "").slice(0, 15) + "Z";
    return [
        "BEGIN:VCALENDAR",
        "VERSION:2.0",
        "PRODID:-//helaasvrij//calendar//EN",
        "CALSCALE:GREGORIAN",
        ...VTIMEZONE,
        "BEGIN:VEVENT",
        `UID:interview-${event.id}@helaasvrij`,
        `DTSTAMP:${stamp}`,
        `DTSTART;TZID=${ZONE}:${icsLocal(event.at)}`,
        `DTEND;TZID=${ZONE}:${icsLocal(end)}`,
        `SUMMARY:${escapeText(event.title)}`,
        `URL:${event.url}`,
        `DESCRIPTION:${escapeText(`Posting: ${event.url}`)}`,
        "END:VEVENT",
        "END:VCALENDAR",
        "",
    ].join("\r\n");
}

/** Saves the interview as an .ics file the student opens in Google or Apple Calendar. */
export function downloadInterviewIcs(event: { id: string; title: string; at: string; postingId: string }) {
    const ics = interviewIcs({ ...event, url: `${window.location.origin}/postings/${event.postingId}` });
    const href = URL.createObjectURL(new Blob([ics], { type: "text/calendar;charset=utf-8" }));
    const a = document.createElement("a");
    a.href = href;
    a.download = `${event.title.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "") || "interview"}.ics`;
    a.click();
    URL.revokeObjectURL(href);
}
