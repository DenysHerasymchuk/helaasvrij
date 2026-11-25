import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate } from "react-router";
import { Plus, SearchLg } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { Input } from "@/components/base/input/input";
import { ApiError, api } from "@/core/api";
import { BoardStatus, useBoard } from "@/features/applications/board-status";
import { AddPostingModal } from "./add-posting-modal";
import { PostingsTabs } from "./postings-tabs";
import { type PostingSummary, formatDate } from "./types";
import { PageHeader } from "@/core/ui";


export function PostingsPage() {
    const navigate = useNavigate();
    const [items, setItems] = useState<PostingSummary[] | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [query, setQuery] = useState("");
    const [debounced, setDebounced] = useState("");
    const [adding, setAdding] = useState(false);
    const board = useBoard();
    const filtered = debounced !== "";
    const shown = items;

    useEffect(() => {
        const timer = setTimeout(() => setDebounced(query.trim()), 250);
        return () => clearTimeout(timer);
    }, [query]);

    const load = useCallback(async () => {
        setError(null);
        setItems(null);
        try {
            const params = new URLSearchParams();
            if (debounced) params.set("q", debounced);
            setItems((await api.get<PostingSummary[]>(`/api/postings?${params}`)) ?? []);
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Something went wrong. Try again.");
        }
    }, [debounced]);

    useEffect(() => {
        load();
    }, [load]);

    return (
        <div className="mx-auto flex w-full max-w-6xl flex-col gap-6 px-4 py-8 md:px-8">
            <PageHeader title="Postings" description="Your saved list: track each posting and apply on your own." />

            <PostingsTabs />

            <div className="flex flex-col items-start gap-3 rounded-xl bg-primary p-4 shadow-xs ring-1 ring-secondary sm:flex-row sm:items-center sm:justify-between sm:p-5">
                <div>
                    <p className="text-md font-semibold text-primary">Found one elsewhere? Add it here.</p>
                    <p className="text-sm text-tertiary">Paste the posting text or a link.</p>
                </div>
                <Button size="md" iconLeading={Plus} onPress={() => setAdding(true)} className="shrink-0">
                    Add posting
                </Button>
            </div>

            <div className="flex flex-col gap-3 sm:flex-row">
                <div className="sm:max-w-sm sm:flex-1">
                    <Input aria-label="Search postings" icon={SearchLg} placeholder="Search company, title or city" value={query} onChange={setQuery} />
                </div>
            </div>

            <section className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
                {error ? (
                    <div className="flex flex-col items-start gap-3 p-6">
                        <p role="alert" className="text-md text-error-primary">
                            {error}
                        </p>
                        <Button color="secondary" size="md" onPress={load}>
                            Try again
                        </Button>
                    </div>
                ) : shown === null ? (
                    <p role="status" className="p-6 text-md text-tertiary">
                        Loading postings...
                    </p>
                ) : shown.length === 0 ? (
                    <div className="flex flex-col items-start gap-3 p-6">
                        <p className="text-md font-semibold text-primary">{filtered ? "No postings match." : "No postings yet."}</p>
                        <p className="text-md text-tertiary">
                            {filtered ? "Try another search." : "Save a posting from Find positions or add your own."}
                        </p>
                        {!filtered && (
                            <Link to="/postings" className="text-md font-semibold text-brand-secondary outline-focus-ring hover:underline focus-visible:rounded-sm focus-visible:outline-2">
                                Find positions
                            </Link>
                        )}
                    </div>
                ) : (
                    <ul className="flex flex-col divide-y divide-secondary">
                        {shown.map((p) => (
                            <li key={p.id} className="flex flex-col md:flex-row md:items-center">
                                <button
                                    type="button"
                                    onClick={() => navigate(`/postings/${p.id}`)}
                                    className="flex min-w-0 flex-1 cursor-pointer flex-col gap-2 px-5 py-4 text-left outline-focus-ring transition hover:bg-primary_hover focus-visible:outline-2 md:flex-row md:items-center md:gap-6"
                                >
                                    <div className="min-w-0 flex-1">
                                        <p className="truncate text-md font-semibold text-primary">{p.title}</p>
                                        <p className="truncate text-sm text-tertiary">
                                            {p.company}
                                            {p.city && `, ${p.city}`}
                                        </p>
                                    </div>
                                    <div className="flex flex-wrap items-center gap-2 md:justify-end">
                                        <span className="text-sm text-tertiary">{p.deadline ? `Deadline ${formatDate(p.deadline)}` : "No deadline"}</span>
                                        {p.analysed && p.score != null ? (
                                            <span className="w-28 text-sm font-semibold text-primary md:text-right">{p.score}% match</span>
                                        ) : (
                                            <span className="w-28 text-sm text-quaternary md:text-right">Not checked yet</span>
                                        )}
                                    </div>
                                </button>
                                <div className="flex flex-wrap items-center gap-3 px-5 pb-4 md:w-80 md:shrink-0 md:py-4 md:pl-0">
                                    <Button size="sm" color="secondary" onClick={() => navigate(`/postings/${p.id}${p.analysed ? "" : "?review=1"}`)}>
                                        {p.analysed ? "See what I miss" : "What am I missing?"}
                                    </Button>
                                    <BoardStatus postingId={p.id} byPosting={board.byPosting} onAdded={board.added} />
                                </div>
                            </li>
                        ))}
                    </ul>
                )}
            </section>

            <AddPostingModal
                isOpen={adding}
                onClose={() => setAdding(false)}
                onAdded={(posting) => {
                    setAdding(false);
                    navigate(`/postings/${posting.id}`);
                }}
            />
        </div>
    );
}
