import { Link } from "react-router";
import { cx } from "@/utils/cx";
import { actions, fmt } from "./dashboard-data";
import { HeaderLink, Panel, rowLink } from "./panel";

export function NextStepsCard() {
    return (
        <Panel
            title="Next steps"
            description="Small projects that close your gaps, biggest gain first."
            className="lg:col-span-2"
            order={5}
            action={<HeaderLink href="/gaps">Full plan</HeaderLink>}
        >
            <ol className="flex flex-col divide-y divide-secondary">
                {actions.slice(0, 5).map((a) => (
                    <li key={a.action}>
                        <Link to="/gaps" className={cx(rowLink, "items-start gap-4")}>
                            <div className="flex min-w-0 flex-1 flex-col gap-1">
                                <p className="text-sm font-medium text-primary">{a.action}</p>
                                <p className="text-sm text-tertiary">
                                    In <span className="font-mono text-secondary">{a.project}</span>, due {fmt(a.due!)}
                                </p>
                            </div>
                            <p className="shrink-0 text-right text-sm text-tertiary">
                                <span className="block font-semibold text-success-primary tabular-nums">+{a.gain}%</span>
                                {a.company}
                            </p>
                        </Link>
                    </li>
                ))}
            </ol>
        </Panel>
    );
}
