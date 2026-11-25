import type { ReactNode } from "react";
import { cardClass } from "@/core/ui";
import { cx } from "@/utils/cx";

/** Card of rows separated by hairlines, like monoself's settings. */
export function SettingsCard({ title, description, tone, children }: { title?: string; description?: string; tone?: "danger"; children: ReactNode }) {
    return (
        <section className={cx(cardClass, tone === "danger" && "ring-error_subtle")}>
            {title && (
                <header className="border-b border-secondary px-5 py-4">
                    <h2 className="font-display text-md font-semibold text-primary">{title}</h2>
                    {description && <p className="mt-0.5 text-sm text-tertiary">{description}</p>}
                </header>
            )}
            <div className="divide-y divide-secondary">{children}</div>
        </section>
    );
}

/** Title and hint on the left, control on the right; stacks on phones. */
export function SettingsRow({ title, hint, children }: { title: ReactNode; hint?: ReactNode; children: ReactNode }) {
    return (
        <div className="flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:gap-6">
            <div className="min-w-0">
                <div className="text-sm font-medium text-primary">{title}</div>
                {hint && <p className="mt-0.5 text-sm text-tertiary">{hint}</p>}
            </div>
            <div className="shrink-0">{children}</div>
        </div>
    );
}
