import { CheckCircle, Circle } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { metaClass } from "@/core/ui";
import type { ProfileItemKind } from "@/features/profile/api";
import { Panel } from "./panel";
import type { Home } from "./use-home";

const kinds: { kind: ProfileItemKind; label: string }[] = [
    { kind: "Skill", label: "skills" },
    { kind: "Project", label: "projects" },
    { kind: "Experience", label: "experience" },
    { kind: "Certification", label: "certificates" },
];

/** How much the app knows about you: connected sources and what was found in them. */
export function ProfileCard({ home }: { home: Home }) {
    const sources = home.dashboard.sources;
    const connected = sources.filter((s) => s.synced).length;
    const items = home.profile?.items ?? [];

    return (
        <Panel title="Your profile" description={`${connected} of 3 sources connected.`} order={4}>
            <div className="flex flex-col gap-3 px-5 py-4">
                <ul className="flex flex-wrap gap-2">
                    {sources.map((s) => (
                        <li key={s.kind} className="flex items-center gap-1.5 rounded-full px-3 py-1 text-sm ring-1 ring-secondary">
                            {s.synced ? <CheckCircle className="size-4 text-fg-success-primary" aria-hidden="true" /> : <Circle className="size-4 text-fg-quaternary" aria-hidden="true" />}
                            <span className={s.synced ? "text-primary" : "text-tertiary"}>{s.kind}</span>
                            <span className="sr-only">{s.synced ? " connected" : " not connected"}</span>
                        </li>
                    ))}
                </ul>
                <p className={metaClass}>
                    {items.length === 0
                        ? "Nothing read from your sources yet."
                        : kinds
                              .map((k) => ({ ...k, n: items.filter((i) => i.kind === k.kind).length }))
                              .filter((k) => k.n > 0)
                              .map((k) => `${k.n} ${k.label}`)
                              .join(", ")}
                </p>
                {(connected < 2 || items.length === 0) && (
                    <div>
                        <Button size="sm" color="secondary" href="/settings/profile">
                            {connected === 0 ? "Connect your sources" : items.length === 0 ? "Read my sources" : "Add another source"}
                        </Button>
                    </div>
                )}
            </div>
        </Panel>
    );
}
