import { ChevronRight } from "@untitledui/icons";
import { Link } from "react-router";
import { itemsInCard, metaClass } from "@/core/ui";
import type { PostingSummary } from "@/features/postings/types";
import { cx } from "@/utils/cx";
import { HeaderLink, Panel, rowLink } from "./panel";
import { ScoreRing } from "./score-ring";

/** The checked positions that fit you best. Positions that were never checked have no match yet, so they are left out. */
export function BestMatchesCard({ postings }: { postings: PostingSummary[] }) {
    const best = postings.filter((p) => p.analysed && p.score != null).sort((a, b) => b.score! - a.score!).slice(0, 3);

    return (
        <Panel title="Best matches" description="The share of requirements you can prove." order={3} action={<HeaderLink href="/list">All</HeaderLink>}>
            {best.length === 0 ? (
                <p className={`${metaClass} px-5 py-4`}>No position is checked yet. Check one to see how well it fits you.</p>
            ) : (
                <ul className={itemsInCard}>
                    {best.map((p) => (
                        <li key={p.id} className="bg-primary">
                            <Link to={`/positions/${p.id}`} className={cx(rowLink, "gap-3 py-2")}>
                                <ScoreRing value={p.score!} size={44} />
                                <div className="flex min-w-0 flex-1 flex-col">
                                    <p className="truncate text-sm font-semibold text-primary">{p.title}</p>
                                    <p className="truncate text-sm text-tertiary">{p.company}</p>
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
