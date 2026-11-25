import { useEffect, useState } from "react";
import { Checkbox } from "@/components/base/checkbox/checkbox";
import { api } from "@/core/api";
import { SettingsCard } from "./settings-row";
import { useSettings } from "./settings-layout";

type Skill = { id: string; name: string; description: string };

const MAX = 4;

function Area({ title, hint, skills, chosen, onChange }: { title: string; hint: string; skills: Skill[]; chosen: string[]; onChange: (ids: string[]) => void }) {
    const toggle = (id: string, on: boolean) => onChange(on ? [...chosen, id] : chosen.filter((c) => c !== id));
    return (
        <fieldset className="flex flex-col gap-3 px-5 py-4">
            <legend className="text-sm font-medium text-primary">{title}</legend>
            <p className="-mt-1 text-sm text-tertiary">{hint}</p>
            <div className="grid gap-3 sm:grid-cols-2">
                {skills.map((s) => (
                    <Checkbox
                        key={s.id}
                        size="md"
                        label={s.name}
                        hint={s.description}
                        isSelected={chosen.includes(s.id)}
                        isDisabled={!chosen.includes(s.id) && chosen.length >= MAX}
                        onChange={(on) => toggle(s.id, on)}
                    />
                ))}
            </div>
        </fieldset>
    );
}

/** Which writing skills the AI uses, per place it writes for you. Saved with the rest of the settings. */
export function WritingSkillsCard() {
    const { draft, update } = useSettings();
    const [skills, setSkills] = useState<Skill[] | null>(null);

    useEffect(() => {
        api.get<Skill[]>("/api/settings/writing-skills")
            .then((list) => setSkills(list ?? []))
            .catch(() => setSkills([]));
    }, []);

    if (!skills || skills.length === 0) return null;

    return (
        <SettingsCard title="Writing skills" description="Style guidance the AI follows when it writes for you. It only changes the wording, never the facts.">
            <Area
                title="CV, cover letters and messages"
                hint="Used for every document the AI writes."
                skills={skills}
                chosen={draft.documentSkills}
                onChange={(documentSkills) => update({ documentSkills })}
            />
            <Area
                title="Interview questions and feedback"
                hint="Used when the AI writes your practice questions and reviews your answers."
                skills={skills}
                chosen={draft.interviewSkills}
                onChange={(interviewSkills) => update({ interviewSkills })}
            />
        </SettingsCard>
    );
}
