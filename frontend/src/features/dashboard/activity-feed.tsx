import { Link } from "react-router";
import { stageLabel } from "@/features/applications/types";
import { itemsInCard, metaClass } from "@/core/ui";
import { Panel } from "./panel";
import type { Activity } from "./use-home";

const MIN = 60_000;

/** "5 min ago", "3 h ago", "2 days ago". */
export function ago(iso: string): string {
    const diff = Date.now() - new Date(iso).getTime();
    if (diff < MIN) return "just now";
    if (diff < 60 * MIN) return `${Math.floor(diff / MIN)} min ago`;
    if (diff < 24 * 60 * MIN) return `${Math.floor(diff / (60 * MIN))} h ago`;
    const days = Math.floor(diff / (24 * 60 * MIN));
    return days === 1 ? "yesterday" : `${days} days ago`;
}

function describe(a: Activity): string {
    if (a.type === "stageChanged") return `Moved to ${a.toStage ? stageLabel(a.toStage) : "a new column"}`;
    if (a.type === "added") return a.sprintName ? `Added to ${a.sprintName}` : "Added to the backlog";
    return a.sprintName ? `Moved into ${a.sprintName}` : "Moved back to the backlog";
}

/** The latest things that happened to your positions. */
export function ActivityFeed({ items }: { items: Activity[] }) {
    return (
        <Panel title="Recent activity" description="The latest moves across your positions." className="min-h-0 overflow-hidden" order={5}>
            {items.length === 0 ? (
                <p className={`${metaClass} px-5 py-4`}>Nothing yet. Moves on the board show up here.</p>
            ) : (
                <ul className={`${itemsInCard} min-h-0 overflow-y-auto`}>
                    {items.map((a, i) => (
                        <li key={`${a.at}-${i}`} className="bg-primary">
                            <Link to={`/positions/${a.postingId}`} className="flex items-center justify-between gap-3 px-3 py-2 outline-focus-ring hover:bg-primary_hover focus-visible:outline-2 focus-visible:-outline-offset-2">
                                <span className="min-w-0">
                                    <span className="block truncate text-sm font-medium text-primary">
                                        {a.title} <span className="font-normal text-tertiary">at {a.company}</span>
                                    </span>
                                    <span className="block truncate text-sm text-tertiary">{describe(a)}</span>
                                </span>
                                <time dateTime={a.at} className="shrink-0 text-xs text-quaternary">
                                    {ago(a.at)}
                                </time>
                            </Link>
                        </li>
                    ))}
                </ul>
            )}
        </Panel>
    );
}
