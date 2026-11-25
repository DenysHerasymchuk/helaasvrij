import { type Ref } from "react";
import { Keyboard01, SearchLg } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { Input } from "@/components/base/input/input";
import { NativeSelect } from "@/features/postings/fields";
import { cx } from "@/utils/cx";
import type { Group, Sort, ViewOptions } from "./view-options";

function Toggle({ on, onChange, children }: { on: boolean; onChange: (on: boolean) => void; children: React.ReactNode }) {
    return (
        <button
            type="button"
            aria-pressed={on}
            onClick={() => onChange(!on)}
            className={cx(
                "cursor-pointer rounded-full px-3 py-1.5 text-sm font-medium outline-focus-ring ring-1 ring-inset transition focus-visible:outline-2",
                on ? "bg-brand-solid text-white ring-brand-solid" : "bg-primary text-secondary ring-secondary hover:bg-primary_hover",
            )}
        >
            {children}
        </button>
    );
}

type Props = {
    options: ViewOptions;
    update: (change: Partial<ViewOptions>) => void;
    companies: string[];
    searchRef: Ref<HTMLInputElement>;
    onHelp: () => void;
};

/** Search, filters, sort and grouping for the board. */
export function BoardToolbar({ options: o, update, companies, searchRef, onHelp }: Props) {
    return (
        <div className="flex flex-wrap items-center gap-2">
            <div className="w-full sm:w-64">
                <Input ref={searchRef} aria-label="Search the board" icon={SearchLg} placeholder="Search (press /)" value={o.query} onChange={(query) => update({ query })} />
            </div>
            <Toggle on={o.soon} onChange={(soon) => update({ soon })}>
                Due within 7 days
            </Toggle>
            <Toggle on={o.strong} onChange={(strong) => update({ strong })}>
                Match 70%+
            </Toggle>
            <Toggle on={o.notes} onChange={(notes) => update({ notes })}>
                Has notes
            </Toggle>
            {companies.length > 1 && (
                <div className="w-40">
                    <NativeSelect aria-label="Company" value={o.company} onChange={(e) => update({ company: e.target.value })}>
                        <option value="">All companies</option>
                        {companies.map((c) => (
                            <option key={c} value={c}>
                                {c}
                            </option>
                        ))}
                    </NativeSelect>
                </div>
            )}
            <div className="w-36">
                <NativeSelect aria-label="Sort cards" value={o.sort} onChange={(e) => update({ sort: e.target.value as Sort })}>
                    <option value="recent">Newest move</option>
                    <option value="match">Best match</option>
                    <option value="deadline">Nearest deadline</option>
                </NativeSelect>
            </div>
            <div className="w-36">
                <NativeSelect aria-label="Group cards" value={o.group} onChange={(e) => update({ group: e.target.value as Group })}>
                    <option value="none">No grouping</option>
                    <option value="company">By company</option>
                </NativeSelect>
            </div>
            <Button size="md" color="secondary" iconLeading={Keyboard01} aria-label="Keyboard shortcuts" className="ml-auto" onPress={onHelp} />
        </div>
    );
}
