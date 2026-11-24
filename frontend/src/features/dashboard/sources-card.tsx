import { CheckCircle, FileCheck02, Globe01 } from "@untitledui/icons";
import { FaGithub } from "react-icons/fa6";
import { Button } from "@/components/base/buttons/button";
import { HeaderLink, Panel } from "./panel";
import type { DashboardSource } from "./use-dashboard";

export function SourcesCard({ sources }: { sources: DashboardSource[] }) {
    return (
        <Panel title="Your sources" description="We read these to check what you can prove." order={2} action={<HeaderLink href="/profile">Manage</HeaderLink>}>
            <ul className="flex flex-col divide-y divide-secondary">
                {sources.map((s) => {
                    const Icon = s.kind === "GitHub" ? FaGithub : s.kind === "CV" ? FileCheck02 : Globe01;
                    return (
                        <li key={s.kind} className="flex items-center gap-3 px-5 py-3">
                            <Icon className="size-5 shrink-0 text-fg-secondary" aria-hidden="true" />
                            <div className="min-w-0 flex-1">
                                <p className="text-sm font-medium text-primary">{s.kind}</p>
                                <p className="truncate text-sm text-tertiary">{s.label}</p>
                            </div>
                            {s.synced ? (
                                <CheckCircle className="size-5 text-fg-success-secondary" aria-label="Connected" />
                            ) : (
                                <Button color="link-color" size="sm" href="/profile">
                                    Add
                                </Button>
                            )}
                        </li>
                    );
                })}
            </ul>
        </Panel>
    );
}
