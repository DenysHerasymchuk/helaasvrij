import { Calendar } from "@untitledui/icons";
import { fmt, nextDeadline, nextInterview, timeFmt } from "./dashboard-data";
import { Panel } from "./panel";
import type { DashboardPosting } from "./use-dashboard";

export function ComingUpCard({ postings }: { postings: DashboardPosting[] }) {
    const interview = nextInterview(postings);
    const deadline = nextDeadline(postings);

    return (
        <Panel title="Coming up" description="Your next interview and the closest deadline." order={3}>
            {!interview && !deadline ? (
                <p className="px-5 py-6 text-sm text-tertiary">Nothing coming up. Add a deadline to a posting.</p>
            ) : (
                <ul className="flex flex-col divide-y divide-secondary">
                    {interview && (
                        <li className="flex gap-3 px-5 py-4">
                            <Calendar className="mt-0.5 size-5 shrink-0 text-fg-brand-secondary" aria-hidden="true" />
                            <div>
                                <p className="text-sm font-medium text-primary">Interview at {interview.company}</p>
                                <p className="text-sm text-tertiary">
                                    {fmt(interview.interviewAt)} at {timeFmt.format(new Date(interview.interviewAt))}, {interview.title}
                                </p>
                            </div>
                        </li>
                    )}
                    {deadline && (
                        <li className="flex gap-3 px-5 py-4">
                            <Calendar className="mt-0.5 size-5 shrink-0 text-fg-quaternary" aria-hidden="true" />
                            <div>
                                <p className="text-sm font-medium text-primary">Apply to {deadline.company}</p>
                                <p className="text-sm text-tertiary">
                                    Before {fmt(deadline.deadline)}, {deadline.title}
                                </p>
                            </div>
                        </li>
                    )}
                </ul>
            )}
        </Panel>
    );
}
