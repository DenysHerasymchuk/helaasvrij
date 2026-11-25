import { NavLink, Outlet, useLocation } from "react-router";
import { PageFrame, pageTitleClass } from "@/core/ui";

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

/** One home for the search, like a Jira project: tabs across the top, the page below. The Board tab fills the window and scrolls inside. */
export function ProjectLayout() {
    const full = useLocation().pathname.startsWith("/board");
    return (
        <PageFrame full={full} width="max-w-[1600px]">
            <header className="flex flex-col gap-3">
                <h1 className={pageTitleClass}>Board</h1>
                <nav aria-label="Board sections" className="flex gap-6 overflow-x-auto overflow-y-hidden border-b border-secondary [scrollbar-width:none]">
                    {tabs.map((t) => (
                        <NavLink key={t.to} to={t.to} className={tab}>
                            {t.label}
                        </NavLink>
                    ))}
                </nav>
            </header>
            <div className="flex min-h-0 flex-1 flex-col gap-4">
                <Outlet />
            </div>
        </PageFrame>
    );
}
