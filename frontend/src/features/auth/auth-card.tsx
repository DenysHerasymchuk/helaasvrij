import type { ReactNode } from "react";
import { Logo } from "@/core/logo";

// Shared card layout from the Untitled UI login/signup templates; logo is the helaasvrij wordmark.
export function AuthCard({ title, subtitle, children, footer }: { title: string; subtitle: string; children: ReactNode; footer: ReactNode }) {
    return (
        <main className="min-h-dvh bg-primary px-4 py-12 sm:bg-secondary md:px-8 md:pt-24">
            <div className="flex w-full flex-col gap-6 bg-primary sm:mx-auto sm:max-w-110 sm:rounded-2xl sm:px-10 sm:py-8 sm:shadow-sm">
                <div className="flex flex-col items-center gap-6 text-center">
                    <Logo className="h-9 w-auto" />
                    <div className="flex flex-col gap-2 md:gap-3">
                        <h1 className="font-display text-display-xs font-semibold tracking-tight text-primary md:text-display-sm">{title}</h1>
                        <p className="text-md text-tertiary">{subtitle}</p>
                    </div>
                </div>
                {children}
                <div className="flex justify-center gap-1 text-center">{footer}</div>
            </div>
        </main>
    );
}
