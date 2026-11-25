import { NavLink } from "react-router";

const tab = ({ isActive }: { isActive: boolean }) =>
    `-mb-px flex shrink-0 items-center gap-2 border-b-2 px-1 pb-3 text-sm font-semibold whitespace-nowrap outline-focus-ring transition focus-visible:rounded-sm focus-visible:outline-2 ${
        isActive ? "border-fg-brand-primary_alt text-brand-secondary" : "border-transparent text-quaternary hover:border-fg-quaternary hover:text-secondary"
    }`;

/** Underline tabs between the backlog and the Adzuna search. NavLink sets aria-current="page". */
export function PostingsTabs() {
    return (
        <nav aria-label="Backlog sections" className="flex gap-6 border-b border-secondary">
            <NavLink to="/backlog" end className={tab}>
                Backlog
            </NavLink>
            <NavLink to="/backlog/find" className={tab}>
                Find positions
            </NavLink>
        </nav>
    );
}
