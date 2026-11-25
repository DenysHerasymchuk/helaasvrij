import { useMemo } from "react";
import { cx } from "@/utils/cx";
import { diffLines } from "./diff";

const style = {
    same: "text-secondary",
    added: "bg-success-primary text-success-primary",
    removed: "bg-error-primary text-error-primary line-through decoration-1",
} as const;

const mark = { same: "", added: "+", removed: "−" } as const;
const word = { same: "unchanged", added: "added", removed: "removed" } as const;

/** What tailoring changed: your original CV text against the tailored one, line by line. */
export function ChangesView({ before, after }: { before: string; after: string }) {
    const lines = useMemo(() => diffLines(before, after), [before, after]);
    const added = lines.filter((l) => l.kind === "added").length;
    const removed = lines.filter((l) => l.kind === "removed").length;

    return (
        <div className="flex flex-col gap-3">
            <p className="text-sm text-tertiary">
                <span className="font-semibold text-success-primary">{added} added</span>,{" "}
                <span className="font-semibold text-error-primary">{removed} removed</span>. Lines you did not change are in grey.
            </p>
            <ul className="flex flex-col gap-1.5 rounded-lg bg-primary p-3 text-sm ring-1 ring-secondary">
                {lines.map((l, i) => (
                    <li key={i} className={cx("flex gap-2 rounded px-2 py-1", style[l.kind])}>
                        <span aria-hidden="true" className="w-3 shrink-0 text-center font-semibold">
                            {mark[l.kind]}
                        </span>
                        <span className="sr-only">{word[l.kind]}: </span>
                        <span className="min-w-0 break-words">{l.text}</span>
                    </li>
                ))}
            </ul>
        </div>
    );
}
