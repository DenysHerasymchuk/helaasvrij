import type { FC, ReactNode } from "react";
import { Link } from "react-router";
import { CheckDone01, ChevronRight, File06, Mail01, MessageChatCircle, Microphone02 } from "@untitledui/icons";

type Step = { to: string; icon: FC<{ className?: string }>; label: string; hint: string };

/** The hub for one analysed posting: every next move links to its page with this posting already chosen. */
export function NextSteps({ postingId, openGaps, board }: { postingId: string; openGaps: number; board: ReactNode }) {
    const id = encodeURIComponent(postingId);
    const steps: Step[] = [
        {
            to: `/gaps?postingId=${id}`,
            icon: CheckDone01,
            label: "Close these gaps",
            hint: openGaps > 0 ? `${openGaps} open ${openGaps === 1 ? "gap" : "gaps"}, each with one step in your own repo.` : "No open gaps. Check the list anyway.",
        },
        { to: `/documents?postingId=${id}&mode=tailor`, icon: File06, label: "Tailor my CV for this posting", hint: "Built only from items you confirmed." },
        { to: `/documents?postingId=${id}&kind=coverLetter`, icon: Mail01, label: "Write a cover letter", hint: "In the language of the posting." },
        { to: `/documents?postingId=${id}&kind=recruiterMessage`, icon: MessageChatCircle, label: "Recruiter message", hint: "Short, max 300 characters. You send it yourself." },
        { to: `/interview?postingId=${id}`, icon: Microphone02, label: "Prep for the interview", hint: "Questions from your gaps and your repos." },
    ];

    return (
        <section aria-labelledby="next-steps-title" className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
            <div className="flex flex-col gap-1 border-b border-secondary px-5 py-4">
                <h2 id="next-steps-title" className="text-md font-semibold text-primary">
                    What to do next
                </h2>
                <p className="text-sm text-tertiary">Close the gaps, apply with documents built for this posting, then track it on your board.</p>
            </div>
            <ul className="grid gap-px bg-secondary sm:grid-cols-2">
                {steps.map(({ to, icon: Icon, label, hint }) => (
                    <li key={to} className="bg-primary">
                        <Link
                            to={to}
                            className="group flex h-full items-center gap-3 px-5 py-4 outline-focus-ring transition hover:bg-primary_hover focus-visible:outline-2 focus-visible:-outline-offset-2"
                        >
                            <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-secondary ring-1 ring-secondary ring-inset">
                                <Icon className="size-5 text-fg-secondary" aria-hidden="true" />
                            </span>
                            <span className="min-w-0 flex-1">
                                <span className="block text-sm font-semibold text-primary">{label}</span>
                                <span className="block text-sm text-tertiary">{hint}</span>
                            </span>
                            <ChevronRight className="size-5 shrink-0 text-fg-quaternary transition group-hover:translate-x-0.5" aria-hidden="true" />
                        </Link>
                    </li>
                ))}
                <li className="flex flex-col justify-center gap-2 bg-primary px-5 py-4">
                    <span className="text-sm font-semibold text-primary">Apply and track</span>
                    {board}
                </li>
            </ul>
        </section>
    );
}
