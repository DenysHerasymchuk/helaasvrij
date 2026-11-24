import { Button } from "@/components/base/buttons/button";

export function ComingSoonPage({ title }: { title: string }) {
    return (
        <div className="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-8 md:px-8">
            <h1 className="text-display-xs font-semibold text-primary">{title}</h1>
            <p className="text-md text-tertiary">This page is not built yet. The dashboard shows a preview of its data.</p>
            <div>
                <Button color="secondary" size="md" href="/dashboard">
                    Back to dashboard
                </Button>
            </div>
        </div>
    );
}
