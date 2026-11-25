import { AlarmClock, CheckCircle, MessageChatCircle, SearchLg, Target04 } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { itemsInCard, metaClass } from "@/core/ui";
import type { FocusItem } from "./focus";
import { Panel } from "./panel";

const icon = { interview: MessageChatCircle, deadline: AlarmClock, gap: Target04, check: SearchLg } as const;
const tint = {
    interview: "bg-brand-primary text-brand-secondary",
    deadline: "bg-warning-primary text-warning-primary",
    gap: "bg-secondary text-secondary",
    check: "bg-secondary text-secondary",
} as const;

/** The to-do list for today. Each item is its own box with its own button. */
export function FocusList({ items }: { items: FocusItem[] }) {
    return (
        <Panel title="Focus today" description="Most urgent first." className="min-h-0 flex-1 overflow-hidden" order={1}>
            {items.length === 0 ? (
                <div className="flex flex-col items-center gap-2 px-5 py-10 text-center">
                    <CheckCircle className="size-8 text-fg-success-primary" aria-hidden="true" />
                    <p className="text-md font-semibold text-primary">You are all caught up.</p>
                    <p className={metaClass}>Add a position or start a sprint to line up your next steps.</p>
                </div>
            ) : (
                <ul className={`${itemsInCard} min-h-0 overflow-y-auto`}>
                    {items.map((i) => {
                        const Icon = icon[i.tone];
                        return (
                            <li key={i.id} className="flex items-center gap-3 bg-primary px-3 py-2.5">
                                <span className={`flex size-9 shrink-0 items-center justify-center rounded-lg ${tint[i.tone]}`}>
                                    <Icon className="size-5" aria-hidden="true" />
                                </span>
                                <div className="min-w-0 flex-1">
                                    <p className="truncate text-sm font-semibold text-primary">{i.title}</p>
                                    <p className="truncate text-sm text-tertiary">{i.detail}</p>
                                </div>
                                <Button size="sm" color="secondary" href={i.to} className="shrink-0">
                                    {i.cta}
                                </Button>
                            </li>
                        );
                    })}
                </ul>
            )}
        </Panel>
    );
}
