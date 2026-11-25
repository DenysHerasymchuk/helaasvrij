import { Link } from "react-router";
import { File06, FileCheck02, MessageChatCircle, Plus, SearchLg, Target04 } from "@untitledui/icons";
import { cardClass } from "@/core/ui";

const shortcuts = [
    { to: "/backlog", icon: Plus, title: "Add a position", hint: "Paste a posting or a link into your backlog." },
    { to: "/backlog/find", icon: SearchLg, title: "Find positions", hint: "Search open positions in Belgium." },
    { to: "/gaps", icon: Target04, title: "Close gaps", hint: "One checklist of what each position asks that you cannot show yet." },
    { to: "/documents", icon: FileCheck02, title: "Tailor your CV", hint: "Rewrite your CV for one position, using only what you can prove." },
    { to: "/documents", icon: File06, title: "Write a cover letter", hint: "A letter or recruiter message built from your own work." },
    { to: "/interview", icon: MessageChatCircle, title: "Practice interviews", hint: "Questions from your gaps and your projects, with feedback." },
];

/** Everything you can do from here, as one grid of shortcuts. */
export function MenuTab() {
    return (
        <nav aria-label="Shortcuts" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {shortcuts.map(({ to, icon: Icon, title, hint }) => (
                <Link
                    key={title}
                    to={to}
                    className={`${cardClass} flex flex-col gap-3 p-5 outline-focus-ring transition hover:bg-primary_hover focus-visible:outline-2`}
                >
                    <Icon className="size-6 text-fg-brand-primary_alt" aria-hidden="true" />
                    <span className="text-md font-semibold text-primary">{title}</span>
                    <span className="text-sm text-tertiary">{hint}</span>
                </Link>
            ))}
        </nav>
    );
}
