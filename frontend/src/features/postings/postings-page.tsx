import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { Plus, SearchLg } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { Input } from "@/components/base/input/input";
import { ApiError, api } from "@/core/api";
import { BoardStatus, useBoard } from "@/features/applications/board-status";
import { AddPostingModal } from "./add-posting-modal";
import { type PostingSummary, formatDate } from "./types";


export function PostingsPage() {
    const navigate = useNavigate();
    const [items, setItems] = useState<PostingSummary[] | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [query, setQuery] = useState("");
    const [debounced, setDebounced] = useState("");
    const [adding, setAdding] = useState(false);
    const board = useBoard();
    const filtered = debounced !== "";

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
        <div className="mx-auto flex max-w-6xl flex-col gap-6 px-4 py-8 md:px-8">
            <div className="flex flex-wrap items-center justify-between gap-4">
                <div>
                    <h1 className="text-display-xs font-semibold text-primary">Postings</h1>
                    <p className="mt-1 text-md text-tertiary">Internships you want to apply for.</p>
                </div>
                <Button size="md" iconLeading={Plus} onPress={() => setAdding(true)}>
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
                ) : items === null ? (
                    <p role="status" className="p-6 text-md text-tertiary">
                        Loading postings...
                    </p>
                ) : items.length === 0 ? (
                    <div className="flex flex-col items-start gap-3 p-6">
                        <p className="text-md font-semibold text-primary">{filtered ? "No postings match." : "No postings yet."}</p>
                        <p className="text-md text-tertiary">
                            {filtered ? "Try another search." : "Paste a posting or add a link to see what it asks for."}
                        </p>
                        {!filtered && (
                            <Button size="md" iconLeading={Plus} onPress={() => setAdding(true)}>
                                Add posting
                            </Button>
                        )}
                    </div>
                ) : (
                    <ul className="flex flex-col divide-y divide-secondary">
                        {items.map((p) => (
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
                                        <span className="w-28 text-sm text-quaternary md:text-right">Not analysed</span>
                                    </div>
                                </button>
                                <div className="px-5 pb-4 md:w-64 md:shrink-0 md:py-4 md:pl-0">
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
