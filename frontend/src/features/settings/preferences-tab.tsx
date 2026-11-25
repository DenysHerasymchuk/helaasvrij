import { useTheme } from "@/core/theme";
import { Segmented } from "./segmented";
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

    return (
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
        </SettingsCard>
    );
}
