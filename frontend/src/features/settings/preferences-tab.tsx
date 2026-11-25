import { useDensity } from "@/core/density";
import { useTheme } from "@/core/theme";
import { Segmented } from "./segmented";
import { BookmarkCard } from "./bookmark-card";
import { WritingSkillsCard } from "./writing-skills-card";
import { SettingsCard, SettingsRow } from "./settings-row";
import { type Language, useSettings } from "./settings-layout";

const languages: { value: Language; label: string }[] = [
    { value: "nl", label: "Nederlands" },
    { value: "fr", label: "Français" },
    { value: "en", label: "English" },
];

export function PreferencesTab() {
    const { draft, update } = useSettings();
    const { theme, toggle } = useTheme();
    const { density, choose } = useDensity();

    return (
        <div className="flex flex-col gap-6">
        <SettingsCard>
            <SettingsRow title="Default language" hint="Used for new documents when a posting does not set a language.">
                <Segmented label="Default language" value={draft.defaultLanguage} options={languages} onChange={(defaultLanguage) => update({ defaultLanguage })} />
            </SettingsRow>
            <SettingsRow title="Appearance" hint="Applies right away on this device.">
                <Segmented
                    label="Appearance"
                    value={theme}
                    options={[
                        { value: "light", label: "Light" },
                        { value: "dark", label: "Dark" },
                    ]}
                    onChange={(next) => next !== theme && toggle()}
                />
            </SettingsRow>
            <SettingsRow title="Size" hint="How big text and spacing are across the app. Applies right away on this device.">
                <Segmented
                    label="Size"
                    value={density}
                    options={[
                        { value: "compact", label: "Compact" },
                        { value: "default", label: "Default" },
                        { value: "roomy", label: "Roomy" },
                        { value: "large", label: "Large" },
                    ]}
                    onChange={choose}
                />
            </SettingsRow>
        </SettingsCard>
        <WritingSkillsCard />
        <BookmarkCard />
        </div>
    );
}
