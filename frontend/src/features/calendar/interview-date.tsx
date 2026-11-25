import { useEffect, useState } from "react";
import { CalendarPlus01, Edit03, MessageChatCircle } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { BelgianDateInput, DateField, Field } from "@/features/postings/fields";
import type { PostingDetail } from "@/features/postings/types";
import { brusselsParts, brusselsToIso, downloadInterviewIcs, formatBrussels, todayInBrussels } from "./brussels";

type Props = {
    postingId: string;
    company: string;
    /** Open the "When is the interview?" form at once when no date is set (the card just moved to Interview). */
    autoOpen?: boolean;
};

/** Interview date for one posting: shows it, asks for it, and offers the .ics download. Saves through the posting dates endpoint. */
export function InterviewDate({ postingId, company, autoOpen = false }: Props) {
    const [posting, setPosting] = useState<PostingDetail | null>(null);
    const [failed, setFailed] = useState(false);
    const [open, setOpen] = useState(false);
    const [date, setDate] = useState("");
    const [time, setTime] = useState("10:00");
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        let alive = true;
        api.get<PostingDetail>(`/api/postings/${postingId}`)
            .then((p) => {
                if (!alive || !p) return;
                setPosting(p);
                if (!p.interviewAt && autoOpen) setOpen(true);
            })
            .catch(() => alive && setFailed(true));
        return () => {
            alive = false;
        };
        // autoOpen only matters for the first load.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [postingId]);

    if (failed) return <p className="text-xs text-tertiary">Could not load the interview date.</p>;
    if (!posting) return null;

    function startEdit() {
        const current = posting?.interviewAt ? brusselsParts(posting.interviewAt) : null;
        setDate(current?.date ?? "");
        setTime(current?.time ?? "10:00");
        setError(null);
        setOpen(true);
    }

    async function save(interviewAt: string | null) {
        if (!posting) return;
        setSaving(true);
        setError(null);
        try {
            // PUT replaces every date, so the other dates go back unchanged.
            const updated = await api.put<PostingDetail>(`/api/postings/${postingId}`, {
                deadline: posting.deadline,
                interviewAt,
            });
            if (updated) setPosting(updated);
            setOpen(false);
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Could not save the date. Try again.");
        } finally {
            setSaving(false);
        }
    }

    if (open) {
        return (
            <form
                className="flex flex-col gap-2 rounded-lg bg-secondary p-3 ring-1 ring-secondary"
                onSubmit={(e) => {
                    e.preventDefault();
                    if (!date) return setError("Pick a date, or skip for now.");
                    save(brusselsToIso(date, time || "10:00"));
                }}
            >
                <p className="text-sm font-semibold text-primary">When is the interview?</p>
                <BelgianDateInput label="Date" value={date} onChange={setDate} min={posting.interviewAt ? undefined : todayInBrussels()} />
                <Field label="Time (Belgian time)">
                    <DateField type="time" value={time} onChange={(e) => setTime(e.target.value)} step={300} />
                </Field>
                {error && (
                    <p role="alert" className="text-sm text-error-primary">
                        {error}
                    </p>
                )}
                <div className="flex flex-wrap gap-2">
                    <Button type="submit" size="sm" isLoading={saving}>
                        Save date
                    </Button>
                    <Button size="sm" color="secondary" isDisabled={saving} onPress={() => setOpen(false)}>
                        {posting.interviewAt ? "Cancel" : "Skip for now"}
                    </Button>
                    {posting.interviewAt && (
                        <Button size="sm" color="link-gray" isDisabled={saving} onPress={() => save(null)}>
                            Remove date
                        </Button>
                    )}
                </div>
            </form>
        );
    }

    if (!posting.interviewAt) {
        return (
            <Button color="link-color" size="sm" iconLeading={MessageChatCircle} onPress={startEdit} className="self-start">
                Add interview date
            </Button>
        );
    }

    const at = posting.interviewAt;
    return (
        <div className="flex flex-col gap-1">
            <p className="flex items-center gap-1.5 text-sm font-medium text-primary">
                <MessageChatCircle className="size-4 shrink-0 text-fg-warning-secondary" aria-hidden="true" />
                Interview {formatBrussels(at)}
            </p>
            <div className="flex flex-wrap gap-x-3 gap-y-1">
                <Button color="link-gray" size="sm" iconLeading={Edit03} onPress={startEdit}>
                    Change
                </Button>
                <Button
                    color="link-gray"
                    size="sm"
                    iconLeading={CalendarPlus01}
                    onPress={() => downloadInterviewIcs({ id: postingId, title: `Interview at ${company}`, at, postingId })}
                >
                    Add to my calendar
                </Button>
            </div>
        </div>
    );
}
