import { ChevronRight } from "@untitledui/icons";
import { Link } from "react-router";
import { score } from "@/features/demo/demo-user";
import { cx } from "@/utils/cx";
import { fmt, ranked } from "./dashboard-data";
import { HeaderLink, Panel, rowLink } from "./panel";
import { ScoreRing } from "./score-ring";

export function BestMatchesCard() {
    return (
        <Panel
            title="Postings that fit you best"
            description="The share of each posting's requirements you can prove. Required skills count double."
            className="lg:col-span-3"
            order={4}
            action={<HeaderLink href="/postings">All postings</HeaderLink>}
        >
            <ul className="flex flex-col divide-y divide-secondary">
                {ranked.slice(0, 3).map((p) => (
                    <li key={p.id}>
                        <Link to="/postings" className={cx(rowLink, "gap-5 py-4")}>
                            <ScoreRing value={score(p.requirements)} />
                            <div className="flex min-w-0 flex-1 flex-col gap-1">
                                <p className="truncate text-sm font-semibold text-primary">{p.title}</p>
                                <p className="text-sm text-tertiary">
                                    {p.company}, {p.city}. Apply by {fmt(p.deadline)}
                                </p>
                            </div>
                            <ChevronRight className="size-5 shrink-0 text-fg-quaternary" aria-hidden="true" />
                        </Link>
                    </li>
                ))}
            </ul>
        </Panel>
    );
}
