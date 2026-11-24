import { Button } from "@/components/base/buttons/button";
import { cx } from "@/utils/cx";

export const rowLink =
    "flex items-center gap-3 px-5 py-3 outline-focus-ring transition-colors hover:bg-primary_hover focus-visible:outline-2 focus-visible:-outline-offset-2";

export function HeaderLink({ href, children }: { href: string; children: React.ReactNode }) {
    return (
        <Button color="link-gray" size="sm" href={href} className="shrink-0">
            {children}
        </Button>
    );
}

// order staggers the one entrance animation (60ms per card).
export function Panel({
    title,
    description,
    action,
    children,
    className,
    order = 0,
}: {
    title: string;
    description?: string;
    action?: React.ReactNode;
    children: React.ReactNode;
    className?: string;
    order?: number;
}) {
    return (
        <section
            className={cx("dash-card flex flex-col rounded-xl bg-primary shadow-xs ring-1 ring-secondary", className)}
            style={{ animationDelay: `${order * 60}ms` }}
        >
            <header className="flex items-start justify-between gap-4 border-b border-secondary px-5 py-4">
                <div className="flex flex-col gap-0.5">
                    <h2 className="text-md font-semibold text-primary">{title}</h2>
                    {description && <p className="text-sm text-tertiary">{description}</p>}
                </div>
                {action}
            </header>
            {children}
        </section>
    );
}
