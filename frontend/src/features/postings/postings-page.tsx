import { useEffect, useState } from "react";
import { Button } from "@/components/base/buttons/button";

type Job = { id: string; title: string; company?: string; location?: string; description?: string; created?: string; url: string };

const field = "w-full rounded-lg bg-primary px-3 py-2 text-md text-primary shadow-xs ring-1 ring-primary outline-none placeholder:text-placeholder focus:ring-2 focus:ring-brand";
const dateFmt = new Intl.DateTimeFormat("en-BE", { day: "numeric", month: "short" });

export function PostingsPage() {
    const [what, setWhat] = useState("stage developer");
    const [where, setWhere] = useState("");
    const [jobs, setJobs] = useState<Job[] | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);

    async function search(e?: React.FormEvent) {
        e?.preventDefault();
        setLoading(true);
        setError(null);
        try {
            const res = await fetch(`/api/jobs?${new URLSearchParams({ what, where })}`, { credentials: "include" });
            if (!res.ok) throw new Error(res.status === 503 ? "Job search is not set up yet (Adzuna keys missing)." : "Could not load postings. Try again.");
            setJobs(await res.json());
        } catch (err) {
            setError(err instanceof Error ? err.message : "Could not load postings. Try again.");
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => {
        void search();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    return (
        <div className="mx-auto flex max-w-4xl flex-col gap-6 px-4 py-8 md:px-8">
            <div className="flex flex-col gap-1">
                <h1 className="text-display-xs font-semibold text-primary">Postings</h1>
                <p className="text-md text-tertiary">Internships in Belgium. Search by role and city.</p>
            </div>

            <form onSubmit={search} className="flex flex-col gap-3 sm:flex-row">
                <input aria-label="Role" className={field} value={what} onChange={(e) => setWhat(e.target.value)} placeholder="Role, e.g. stage developer" />
                <input aria-label="City" className={field} value={where} onChange={(e) => setWhere(e.target.value)} placeholder="City, e.g. Gent" />
                <Button type="submit" isLoading={loading} isDisabled={loading} className="shrink-0">
                    Search
                </Button>
            </form>

            {error && (
                <div className="flex items-center justify-between gap-4 rounded-xl bg-primary px-5 py-4 ring-1 ring-secondary">
                    <p className="text-sm text-error-primary">{error}</p>
                    <Button size="sm" color="secondary" onClick={() => search()}>
                        Try again
                    </Button>
                </div>
            )}

            {jobs && jobs.length === 0 && <p className="text-md text-tertiary">No postings found. Try another role or city.</p>}

            {jobs && jobs.length > 0 && (
                <ul className="flex flex-col divide-y divide-secondary rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
                    {jobs.map((j) => (
                        <li key={j.id} className="flex flex-col gap-1 px-5 py-4">
                            <div className="flex items-start justify-between gap-4">
                                <a href={j.url} target="_blank" rel="noopener noreferrer" className="text-md font-semibold text-primary hover:underline">
                                    {j.title}
                                </a>
                                {j.created && <span className="shrink-0 text-sm text-tertiary">{dateFmt.format(new Date(j.created))}</span>}
                            </div>
                            <p className="text-sm text-secondary">{[j.company, j.location].filter(Boolean).join(", ")}</p>
                            {j.description && <p className="line-clamp-2 text-sm text-tertiary">{j.description}</p>}
                        </li>
                    ))}
                </ul>
            )}

            {/* Required by the Adzuna API terms on every page that shows their ads. */}
            <a href="https://www.adzuna.be" target="_blank" rel="noopener noreferrer" className="self-start text-sm font-semibold text-secondary hover:underline">
                Jobs by Adzuna
            </a>
        </div>
    );
}
