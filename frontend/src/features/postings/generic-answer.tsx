import { useCallback, useEffect, useState } from "react";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { type GenericAnswer, formatDate } from "./types";

/** A generic chatbot answer for the same posting (posting text only), shown next to the gap matrix. */
export function GenericAnswerPanel({ postingId }: { postingId: string }) {
    const [answer, setAnswer] = useState<GenericAnswer | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);

    const load = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            setAnswer(await api.post<GenericAnswer>(`/api/postings/${postingId}/generic-answer`, {}));
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Something went wrong. Try again.");
        } finally {
            setLoading(false);
        }
    }, [postingId]);

    useEffect(() => {
        load();
    }, [load]);

    return (
        <section className="flex flex-col rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
            <div className="flex flex-wrap items-center gap-2 border-b border-secondary px-5 py-4">
                <h2 className="min-w-0 flex-1 text-md font-semibold text-primary">Generic chatbot answer</h2>
                <Badge type="pill-color" size="sm" color="gray">
                    No profile data
                </Badge>
            </div>
            <div className="flex flex-col gap-4 px-5 py-4">
                {loading && (
                    <p role="status" className="text-md text-tertiary">
                        Getting a generic answer...
                    </p>
                )}
                {!loading && error && (
                    <div className="flex flex-col gap-3">
                        <p role="alert" className="text-sm text-error-primary">
                            {error}
                        </p>
                        <div>
                            <Button color="secondary" size="md" onPress={load}>
                                Try again
                            </Button>
                        </div>
                    </div>
                )}
                {!loading && !error && answer && (
                    <>
                        <p className="text-sm text-tertiary">
                            {answer.isDemo
                                ? `Generic chatbot answer, captured ${formatDate(answer.capturedAt)}.`
                                : `Generic chatbot answer, generated ${formatDate(answer.capturedAt)}.`}{" "}
                            It saw only the posting text and the question "What do I need to get this internship?"
                        </p>
                        {/* Model output: plain text only, never HTML. */}
                        <p className="whitespace-pre-wrap text-md text-secondary">{answer.text}</p>
                        <div className="rounded-lg bg-secondary px-4 py-3 text-sm text-secondary">
                            <p className="font-semibold text-primary">What it cannot do</p>
                            <p className="mt-1">It quotes no evidence from your sources, names none of your repos and computes no match score.</p>
                        </div>
                    </>
                )}
            </div>
        </section>
    );
}
