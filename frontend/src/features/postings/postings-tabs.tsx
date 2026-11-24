import { useEffect, useState } from "react";
import { NavLink } from "react-router";
import { api } from "@/core/api";
import type { PostingSummary } from "./types";

const tab = ({ isActive }: { isActive: boolean }) =>
    `-mb-px flex shrink-0 items-center gap-2 border-b-2 px-1 pb-3 text-sm font-semibold whitespace-nowrap outline-focus-ring transition focus-visible:rounded-sm focus-visible:outline-2 ${
        isActive ? "border-fg-brand-primary_alt text-brand-secondary" : "border-transparent text-quaternary hover:border-fg-quaternary hover:text-secondary"
    }`;

/** Underline tabs between the Adzuna search and the student's own saved list. NavLink sets aria-current="page". */
export function PostingsTabs() {
    const [count, setCount] = useState<number | null>(null);

    useEffect(() => {
        api.get<PostingSummary[]>("/api/postings")
            .then((list) => setCount(list?.length ?? 0))
            .catch(() => setCount(null));
    }, []);

    return (
        <nav aria-label="Postings" className="flex gap-6 overflow-x-auto border-b border-secondary">
            <NavLink to="/postings" end className={tab}>
                Find internships
            </NavLink>
            <NavLink to="/postings/saved" className={tab}>
                My postings{count !== null && ` (${count})`}
            </NavLink>
        </nav>
    );
}
