import type { ReactNode } from "react";
import { cx } from "@/utils/cx";

const control =
    "w-full rounded-lg bg-primary px-3.5 py-2.5 text-md text-primary shadow-xs ring-1 ring-primary ring-inset placeholder:text-placeholder focus:outline-hidden focus:ring-2 focus:ring-brand disabled:cursor-not-allowed disabled:bg-disabled_subtle";

/** Native controls styled with the Untitled UI tokens (Untitled UI textarea/select are not installed yet). */
export function Field({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
    return (
        <label className="flex flex-col gap-1.5">
            <span className="text-sm font-medium text-secondary">{label}</span>
            {children}
            {hint && <span className="text-sm text-tertiary">{hint}</span>}
        </label>
    );
}

export function TextArea(props: React.TextareaHTMLAttributes<HTMLTextAreaElement>) {
    return <textarea {...props} className={cx(control, "resize-y", props.className)} />;
}

export function NativeSelect(props: React.SelectHTMLAttributes<HTMLSelectElement>) {
    return <select {...props} className={cx(control, props.className)} />;
}

export function DateField(props: React.InputHTMLAttributes<HTMLInputElement>) {
    return <input {...props} className={cx(control, props.className)} />;
}
