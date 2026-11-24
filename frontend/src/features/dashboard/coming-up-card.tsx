import { Calendar } from "@untitledui/icons";
import { fmt, nextDeadline, nextInterview } from "./dashboard-data";
import { Panel } from "./panel";

export function ComingUpCard() {
    return (
        <Panel title="Coming up" description="Your next interview and the closest deadline." order={3}>
            <ul className="flex flex-col divide-y divide-secondary">
                {nextInterview?.interviewAt && (
                    <li className="flex gap-3 px-5 py-4">
                        <Calendar className="mt-0.5 size-5 shrink-0 text-fg-brand-secondary" aria-hidden="true" />
                        <div>
                            <p className="text-sm font-medium text-primary">Interview at {nextInterview.company}</p>
                            <p className="text-sm text-tertiary">
                                {fmt(nextInterview.interviewAt)} at {nextInterview.interviewAt.slice(11)}, {nextInterview.title}
                            </p>
                        </div>
                    </li>
                )}
                <li className="flex gap-3 px-5 py-4">
                    <Calendar className="mt-0.5 size-5 shrink-0 text-fg-quaternary" aria-hidden="true" />
                    <div>
                        <p className="text-sm font-medium text-primary">Apply to {nextDeadline.company}</p>
                        <p className="text-sm text-tertiary">
                            Before {fmt(nextDeadline.deadline)}, {nextDeadline.title}
                        </p>
                    </div>
                </li>
            </ul>
        </Panel>
    );
}
