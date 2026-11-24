import { useEffect, useState } from "react";
import { Link } from "react-router";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { PostingsTabs } from "@/features/postings/postings-tabs";
import { AdzunaLink, CompanyLogo, type Job, JobFacts } from "./job-meta";

const field = "w-full rounded-lg bg-primary px-3 py-2 text-md text-primary shadow-xs ring-1 ring-primary outline-none placeholder:text-placeholder focus:ring-2 focus:ring-brand";

export function JobsPage() {
    const [what, setWhat] = useState("");
    const [where, setWhere] = useState("");
    const [jobs, setJobs] = useState<Job[] | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);

    async function search(e?: React.FormEvent) {
        e?.preventDefault();
        setLoading(true);
        setError(null);
        try {
            setJobs((await api.get<Job[]>(`/api/jobs?${new URLSearchParams({ what, where })}`)) ?? []);
        } catch (err) {
            setError(err instanceof ApiError ? err.message : "Could not load postings. Try again.");
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
                <p className="text-md text-tertiary">Internships in Belgium. Open one to read it, then save it to see what it asks from you.</p>
            </div>

            <PostingsTabs />

            <form onSubmit={search} className="flex flex-col gap-3 sm:flex-row">
                <input aria-label="Role" className={field} value={what} onChange={(e) => setWhat(e.target.value)} placeholder="Role or keyword, e.g. marketing, IT, .NET" />
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

            {loading && !jobs && (
                <p role="status" className="text-md text-tertiary">
                    Loading postings...
                </p>
            )}

            {jobs && jobs.length === 0 && <p className="text-md text-tertiary">No postings found. Try another role or city.</p>}

            {jobs && jobs.length > 0 && (
                <ul className="flex flex-col divide-y divide-secondary rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
                    {jobs.map((j) => (
                        <li key={j.id} className="relative flex gap-4 px-5 py-4 hover:bg-primary_hover">
                            <CompanyLogo company={j.company} />
                            <div className="flex min-w-0 flex-1 flex-col gap-1.5">
                                {/* The title link covers the whole row; the Adzuna link sits above it. */}
                                <Link
                                    to={`/postings/job/${encodeURIComponent(j.id)}`}
                                    className="rounded-sm text-md font-semibold text-primary outline-focus-ring after:absolute after:inset-0 focus-visible:outline-2 focus-visible:outline-offset-2"
                                >
                                    {j.title}
                                </Link>
                                {j.company && <p className="text-sm font-medium text-secondary">{j.company}</p>}
                                <JobFacts job={j} />
                                {j.description && <p className="line-clamp-2 text-sm text-tertiary">{j.description}</p>}
                                <AdzunaLink className="relative z-10 mt-1 self-start" />
                            </div>
                        </li>
                    ))}
                </ul>
            )}

        </div>
    );
}
