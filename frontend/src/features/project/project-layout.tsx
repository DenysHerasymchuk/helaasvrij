import { NavLink, Outlet } from "react-router";
import { pageTitleClass } from "@/core/ui";

const tab = ({ isActive }: { isActive: boolean }) =>
    `-mb-px flex shrink-0 items-center gap-2 border-b-2 px-1 pb-3 text-sm font-semibold whitespace-nowrap outline-focus-ring transition focus-visible:rounded-sm focus-visible:outline-2 ${
        isActive ? "border-fg-brand-primary_alt text-brand-secondary" : "border-transparent text-quaternary hover:border-fg-quaternary hover:text-secondary"
    }`;

const tabs = [
    { to: "/summary", label: "Summary" },
    { to: "/list", label: "List" },
    { to: "/board", label: "Board" },
    { to: "/backlog", label: "Backlog" },
    { to: "/sprints", label: "Sprints" },
];

/** One home for the search, like a Jira project: tabs across the top, the page below. */
export function ProjectLayout() {
    return (
        <div className="mx-auto flex w-full max-w-[1400px] flex-col gap-6 px-4 py-8 md:px-8">
            <header className="flex flex-col gap-4">
                <h1 className={pageTitleClass}>Board</h1>
                <nav aria-label="Board sections" className="flex gap-6 overflow-x-auto border-b border-secondary">
                    {tabs.map((t) => (
                        <NavLink key={t.to} to={t.to} className={tab}>
                            {t.label}
                        </NavLink>
                    ))}
                </nav>
            </header>
            <Outlet />
        </div>
    );
}
