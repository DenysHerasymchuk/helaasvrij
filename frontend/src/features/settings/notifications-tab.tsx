import { Switch } from "react-aria-components";
import { Badge } from "@/components/base/badges/badges";
import { SettingsCard, SettingsRow } from "./settings-row";
import { useSettings } from "./settings-layout";

export function NotificationsTab() {
    const { draft, update } = useSettings();

    return (
        <SettingsCard>
            <SettingsRow
                title={
                    <span className="flex items-center gap-2">
                        Email me about strong matches
                        <Badge type="pill-color" color="gray" size="sm">
                            Coming soon
                        </Badge>
                    </span>
                }
                hint="Saved now, but no emails are sent yet."
            >
                <Switch isSelected={draft.emailAlerts} onChange={(emailAlerts) => update({ emailAlerts })} aria-label="Email alerts" className="group inline-flex cursor-pointer">
                    <span className="flex h-5 w-9 items-center rounded-full bg-quaternary p-0.5 transition group-data-[selected]:bg-brand-solid group-data-[focus-visible]:outline-2 group-data-[focus-visible]:outline-offset-2 group-data-[focus-visible]:outline-focus-ring">
                        <span className="size-4 rounded-full bg-fg-white shadow-sm transition group-data-[selected]:translate-x-4" />
                    </span>
                </Switch>
            </SettingsRow>
            <SettingsRow title="Strong match threshold" hint="Postings at or above this score count as a strong match.">
                <div className="flex items-center gap-4 sm:w-64">
                    <input
                        type="range"
                        min={0}
                        max={100}
                        step={5}
                        value={draft.strongMatchThreshold}
                        onChange={(event) => update({ strongMatchThreshold: Number(event.target.value) })}
                        aria-label="Strong match threshold"
                        className="h-2 flex-1 cursor-pointer accent-brand-600 dark:accent-brand-400 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-focus-ring"
                    />
                    <output className="w-12 text-right text-lg font-semibold text-primary tabular-nums">{draft.strongMatchThreshold}%</output>
                </div>
            </SettingsRow>
        </SettingsCard>
    );
}
