import { ChevronRight } from "@untitledui/icons";
import { itemsInCard } from "@/core/ui";
import { Link } from "react-router";
import { cx } from "@/utils/cx";
import { fmt, ranked } from "./dashboard-data";
import { HeaderLink, Panel, rowLink } from "./panel";
import { ScoreRing } from "./score-ring";
import type { DashboardPosting } from "./use-dashboard";

export function BestMatchesCard({ postings }: { postings: DashboardPosting[] }) {
    return (
        <Panel
            title="Postings that fit you best"
            description="The share of each posting's requirements you can prove. Required skills count double."
            className="lg:col-span-3"
            order={4}
            action={<HeaderLink href="/postings">All postings</HeaderLink>}
        >
            {postings.length === 0 ? (
                <p className="px-5 py-6 text-sm text-tertiary">No postings yet. Add one from Postings.</p>
            ) : (
            <ul className={itemsInCard}>
                {ranked(postings).slice(0, 3).map((p) => (
                    <li key={p.id}>
                        <Link to="/postings" className={cx(rowLink, "gap-5 py-4")}>
                            <ScoreRing value={p.score} />
                            <div className="flex min-w-0 flex-1 flex-col gap-1">
                                <p className="truncate text-sm font-semibold text-primary">{p.title}</p>
                                <p className="text-sm text-tertiary">
                                    {p.company}, {p.city}{p.deadline && `. Apply by ${fmt(p.deadline)}`}
                                </p>
                            </div>
                            <ChevronRight className="size-5 shrink-0 text-fg-quaternary" aria-hidden="true" />
                        </Link>
                    </li>
                ))}
            </ul>
            )}
        </Panel>
    );
}
