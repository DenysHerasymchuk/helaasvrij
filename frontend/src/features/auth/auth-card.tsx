import type { ReactNode } from "react";

// Shared card layout from the Untitled UI login/signup templates; logo is the text wordmark for now.
export function AuthCard({ title, subtitle, children, footer }: { title: string; subtitle: string; children: ReactNode; footer: ReactNode }) {
    return (
        <main className="min-h-dvh bg-primary px-4 py-12 sm:bg-secondary md:px-8 md:pt-24">
            <div className="flex w-full flex-col gap-6 bg-primary sm:mx-auto sm:max-w-110 sm:rounded-2xl sm:px-10 sm:py-8 sm:shadow-sm">
                <div className="flex flex-col items-center gap-6 text-center">
                    <p className="text-lg font-semibold text-brand-secondary">Internship Coach</p>
                    <div className="flex flex-col gap-2 md:gap-3">
                        <h1 className="text-xl font-semibold text-primary md:text-display-xs">{title}</h1>
                        <p className="text-md text-tertiary">{subtitle}</p>
                    </div>
                </div>
                {children}
                <div className="flex justify-center gap-1 text-center">{footer}</div>
            </div>
        </main>
    );
}
