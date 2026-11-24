import type { FC, ReactNode } from "react";
import { Briefcase01, Calendar, Clock, CurrencyEuro, MarkerPin01, Tag01 } from "@untitledui/icons";
import { Avatar } from "@/components/base/avatar/avatar";
import { Badge } from "@/components/base/badges/badges";
import { hashName } from "@/core/avatar";
import { cx } from "@/utils/cx";

/** One Adzuna ad from the shared snapshot (`GET /api/jobs`). */
export type Job = {
    id: string;
    title: string;
    company?: string | null;
    location?: string | null;
    description?: string | null;
    created?: string | null;
    url: string;
    salaryMin?: number | null;
    salaryMax?: number | null;
    salaryIsPredicted?: boolean;
    contractType?: string | null;
    contractTime?: string | null;
    category?: string | null;
};

// Full class names so Tailwind keeps them; utility colours have their own dark-mode values.
const tones = [
    "bg-utility-blue-100 text-utility-blue-700",
    "bg-utility-green-100 text-utility-green-700",
    "bg-utility-orange-100 text-utility-orange-700",
    "bg-utility-pink-100 text-utility-pink-700",
    "bg-utility-purple-100 text-utility-purple-700",
    "bg-utility-indigo-100 text-utility-indigo-700",
    "bg-utility-sky-100 text-utility-sky-700",
    "bg-utility-yellow-100 text-utility-yellow-700",
];

const initials = (name: string) =>
    name
        .split(/[\s\-&.,/]+/)
        .filter(Boolean)
        .slice(0, 2)
        .map((w) => w[0]!.toUpperCase())
        .join("") || "?";

/** Adzuna has no logos: initials on a colour picked from the company name, so the same company always looks the same. */
export function CompanyLogo({ company, size = "md" }: { company?: string | null; size?: "md" | "lg" | "xl" }) {
    const name = company?.trim() || "?";
    const tone = tones[hashName(name) % tones.length];
    return (
        <Avatar
            size={size}
            rounded={false}
            contentClassName={tone.split(" ")[0]}
            placeholder={<span className={cx(tone.split(" ")[1], "font-semibold", size === "md" ? "text-sm" : "text-lg")}>{initials(name)}</span>}
        />
    );
}

const dateFmt = new Intl.DateTimeFormat("en-BE", { day: "numeric", month: "short", year: "numeric" });
const euro = new Intl.NumberFormat("en-BE", { style: "currency", currency: "EUR", maximumFractionDigits: 0 });
const contractLabels: Record<string, string> = { permanent: "Permanent", contract: "Contract", full_time: "Full-time", part_time: "Part-time" };

export const postedOn = (created?: string | null) => (created && !Number.isNaN(Date.parse(created)) ? dateFmt.format(new Date(created)) : null);

export function salaryRange(j: Job) {
    const { salaryMin: min, salaryMax: max } = j;
    if (!min && !max) return null;
    if (min && max && min !== max) return `${euro.format(min)} to ${euro.format(max)}`;
    return euro.format((min || max)!);
}

function Fact({ icon: Icon, label, children }: { icon: FC<{ className?: string }>; label: string; children: ReactNode }) {
    return (
        <li className="flex items-center gap-1.5">
            <Icon className="size-4 shrink-0 text-fg-quaternary" aria-hidden="true" />
            <span className="sr-only">{label}: </span>
            {children}
        </li>
    );
}

/** Location, posted date, salary, contract and category: whatever Adzuna gave for this ad. */
export function JobFacts({ job, className }: { job: Job; className?: string }) {
    const posted = postedOn(job.created);
    const salary = salaryRange(job);
    const contract = [job.contractType, job.contractTime].map((c) => (c ? contractLabels[c] : null)).filter(Boolean).join(", ");
    return (
        <ul className={cx("flex flex-wrap items-center gap-x-4 gap-y-1.5 text-sm text-tertiary", className)}>
            {job.location && (
                <Fact icon={MarkerPin01} label="Location">
                    {job.location}
                </Fact>
            )}
            {posted && (
                <Fact icon={Calendar} label="Posted">
                    Posted {posted}
                </Fact>
            )}
            {salary && (
                <Fact icon={CurrencyEuro} label="Salary">
                    {salary}
                    {job.salaryIsPredicted && (
                        <Badge type="pill-color" size="sm" color="gray">
                            estimate
                        </Badge>
                    )}
                </Fact>
            )}
            {contract && (
                <Fact icon={job.contractTime ? Clock : Briefcase01} label="Contract">
                    {contract}
                </Fact>
            )}
            {job.category && (
                <Fact icon={Tag01} label="Category">
                    {job.category}
                </Fact>
            )}
        </ul>
    );
}

/** Adzuna's terms: every ad shown carries an "Adzuna" link to adzuna.be, at least 116x23 px. */
export function AdzunaLink({ className }: { className?: string }) {
    return (
        <a
            href="https://www.adzuna.be"
            target="_blank"
            rel="noopener noreferrer"
            className={cx(
                "inline-flex h-6 min-w-[116px] items-center justify-center rounded-md px-2 text-xs font-semibold text-tertiary ring-1 ring-secondary hover:text-secondary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring",
                className,
            )}
        >
            Jobs by Adzuna
        </a>
    );
}
