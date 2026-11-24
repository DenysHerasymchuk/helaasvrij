import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { ArrowLeft, LinkExternal01 } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { AdzunaLink, CompanyLogo, type Job, JobFacts } from "./job-meta";

const safeHref = (url: string) => (/^https?:\/\//i.test(url) ? url : null);

/** One Adzuna ad, read before saving it as your own posting. */
export function JobDetailPage() {
    const { jobId = "" } = useParams();
    const navigate = useNavigate();
    const [job, setJob] = useState<Job | null>(null);
    const [error, setError] = useState<{ message: string; notFound: boolean } | null>(null);
    const [saving, setSaving] = useState(false);
    const [saveError, setSaveError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setError(null);
        setJob(null);
        try {
            setJob(await api.get<Job>(`/api/jobs/${encodeURIComponent(jobId)}`));
        } catch (e) {
            const err = e instanceof ApiError ? e : null;
            setError({ message: err?.message ?? "Could not load this posting. Try again.", notFound: err?.status === 404 });
        }
    }, [jobId]);

    useEffect(() => {
        load();
    }, [load]);

    async function save() {
        setSaving(true);
        setSaveError(null);
        try {
            const saved = await api.post<{ id: string }>("/api/postings/from-job", { jobId });
            navigate(saved ? `/postings/${saved.id}` : "/postings/saved");
        } catch (e) {
            setSaveError(e instanceof ApiError ? e.message : "Could not save this posting. Try again.");
            setSaving(false);
        }
    }

    const back = (
        <div>
            <Button color="link-gray" size="md" iconLeading={ArrowLeft} href="/postings">
                All postings
            </Button>
        </div>
    );

    if (error) {
        return (
            <div className="mx-auto flex max-w-4xl flex-col gap-4 px-4 py-8 md:px-8">
                {back}
                <p role="alert" className="text-md text-error-primary">
                    {error.message}
                </p>
                {!error.notFound && (
                    <div>
                        <Button color="secondary" size="md" onPress={load}>
                            Try again
                        </Button>
                    </div>
                )}
            </div>
        );
    }

    if (!job) {
        return (
            <div className="mx-auto max-w-4xl px-4 py-8 md:px-8">
                <p role="status" className="text-md text-tertiary">
                    Loading posting...
                </p>
            </div>
        );
    }

    const href = safeHref(job.url);

    return (
        <div className="mx-auto flex max-w-4xl flex-col gap-6 px-4 py-8 md:px-8">
            {back}

            <header className="flex flex-col gap-4 sm:flex-row sm:items-start">
                <CompanyLogo company={job.company} size="xl" />
                <div className="flex min-w-0 flex-col gap-2">
                    <h1 className="text-display-xs font-semibold text-primary">{job.title}</h1>
                    {job.company && <p className="text-lg font-medium text-secondary">{job.company}</p>}
                    <JobFacts job={job} />
                </div>
            </header>

            <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
                <Button size="md" onPress={save} isLoading={saving} isDisabled={saving}>
                    Save posting
                </Button>
                {href && (
                    <Button size="md" color="secondary" iconTrailing={LinkExternal01} href={href} target="_blank" rel="noopener noreferrer">
                        Open original ad
                    </Button>
                )}
            </div>
            {saveError && (
                <p role="alert" className="-mt-3 text-sm text-error-primary">
                    {saveError}
                </p>
            )}

            <section className="rounded-xl bg-primary shadow-xs ring-1 ring-secondary">
                <h2 className="border-b border-secondary px-5 py-4 text-md font-semibold text-primary">Description</h2>
                <div className="flex flex-col gap-3 px-5 py-4">
                    {/* Ad text is untrusted: rendered as plain text, never as HTML. */}
                    <p className="max-w-prose whitespace-pre-wrap text-md text-secondary">{job.description || "Adzuna gave no description for this ad."}</p>
                    <p className="text-sm text-tertiary">Adzuna only shares the start of each ad. Open the original ad to read all of it.</p>
                </div>
            </section>

            <AdzunaLink className="self-start" />
        </div>
    );
}
