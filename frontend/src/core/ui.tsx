import type { ReactNode } from "react";
import { Button } from "@/components/base/buttons/button";
import { cx } from "@/utils/cx";

/** One look for native input, select and textarea, matching the Untitled UI Input. */
export const fieldClass =
    "w-full min-w-0 rounded-lg bg-primary px-3 py-2 text-md text-primary shadow-xs ring-1 ring-primary ring-inset outline-none transition-shadow duration-100 placeholder:text-placeholder focus:ring-2 focus:ring-brand disabled:cursor-not-allowed disabled:bg-secondary disabled:opacity-70 aria-invalid:ring-error_subtle aria-invalid:focus:ring-error";

/** Standard surface for cards and panels. */
export const cardClass = "rounded-xl bg-primary shadow-xs ring-1 ring-secondary";

/** Page title style; detail pages with a richer header use it directly. */
export const pageTitleClass = "font-display text-display-xs font-semibold tracking-tight text-balance text-primary md:text-display-sm";

/** One page header: Fraunces title, one line of description, at most one primary action on the right. */
export function PageHeader({ title, description, action }: { title: ReactNode; description?: ReactNode; action?: ReactNode }) {
    return (
        <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
            <div className="flex min-w-0 flex-col gap-1.5">
                <h1 className={pageTitleClass}>{title}</h1>
                {description && <p className="max-w-2xl text-md text-tertiary">{description}</p>}
            </div>
            {action && <div className="flex shrink-0 flex-wrap items-end gap-2">{action}</div>}
        </header>
    );
}

/** Error block with a retry. */
export function ErrorState({ message, onRetry, className }: { message: string; onRetry?: () => void; className?: string }) {
    return (
        <div role="alert" className={cx(cardClass, "flex flex-col items-start gap-3 p-5", className)}>
            <p className="text-sm text-error-primary">{message}</p>
            {onRetry && (
                <Button size="sm" color="secondary" onPress={onRetry}>
                    Try again
                </Button>
            )}
        </div>
    );
}

/** Loading line, announced to screen readers. */
export function LoadingState({ children, className }: { children: ReactNode; className?: string }) {
    return (
        <p role="status" className={cx("text-sm text-tertiary", className)}>
            {children}
        </p>
    );
}
