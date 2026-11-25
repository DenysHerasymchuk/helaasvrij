import { Input } from "@/components/base/input/input";
import { BelgianDateInput, Field, TextArea } from "@/features/postings/fields";
import type { SprintDraft } from "./types";

/** Name, goal and dates of a sprint. Used to start a sprint and to plan the next one. */
export function SprintForm({ draft, onChange }: { draft: SprintDraft; onChange: (draft: SprintDraft) => void }) {
    const set = (patch: Partial<SprintDraft>) => onChange({ ...draft, ...patch });
    return (
        <div className="flex flex-col gap-4">
            <Input label="Name" size="md" value={draft.name} onChange={(name) => set({ name })} maxLength={80} />
            <Field label="Goal" hint="One sentence, for example: Apply to 5 .NET roles">
                <TextArea rows={2} maxLength={300} value={draft.goal} onChange={(e) => set({ goal: e.target.value })} />
            </Field>
            <div className="grid grid-cols-2 gap-3">
                <BelgianDateInput label="Starts" value={draft.startsOn} onChange={(startsOn) => set({ startsOn })} />
                <BelgianDateInput label="Ends" value={draft.endsOn} onChange={(endsOn) => set({ endsOn })} min={draft.startsOn} />
            </div>
        </div>
    );
}
