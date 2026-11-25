import { Radio, RadioGroup } from "react-aria-components";

/** Pill-shaped single choice; active option is filled with Ink. */
export function Segmented<T extends string>({ label, value, options, onChange }: { label: string; value: T; options: { value: T; label: string }[]; onChange: (value: T) => void }) {
    return (
        <RadioGroup aria-label={label} orientation="horizontal" value={value} onChange={(next) => onChange(next as T)} className="inline-flex rounded-full bg-secondary p-0.5 ring-1 ring-secondary ring-inset">
            {options.map((option) => (
                <Radio
                    key={option.value}
                    value={option.value}
                    className="cursor-pointer rounded-full px-3.5 py-1.5 text-sm font-medium text-tertiary outline-hidden transition selected:bg-brand-solid selected:text-white hover:text-primary selected:hover:text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
                >
                    {option.label}
                </Radio>
            ))}
        </RadioGroup>
    );
}
