import { Button } from "@/components/base/buttons/button";
import { PageHeader } from "@/core/ui";

export function ComingSoonPage({ title }: { title: string }) {
    return (
        <div className="mx-auto flex w-full max-w-6xl flex-col gap-4 px-4 py-8 md:px-8">
            <PageHeader title={title} description="This page is not built yet. The dashboard shows a preview of its data." />
            <div>
                <Button color="secondary" size="md" href="/dashboard">
                    Back to dashboard
                </Button>
            </div>
        </div>
    );
}
