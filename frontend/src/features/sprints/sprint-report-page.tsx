import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { ArrowLeft } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { api } from "@/core/api";
import { ErrorState, LoadingState, cardClass, itemsOnPage, pageTitleClass } from "@/core/ui";
import { stages } from "@/features/applications/types";
import { formatDate } from "@/features/postings/types";
import { CompleteSprintModal } from "./complete-sprint-modal";
import { type SprintReport } from "./types";
import { errorMessage, useSprints } from "./use-sprints";

function Tile({ label, value }: { label: string; value: string | number }) {
    return (
        <div className={`${cardClass} flex flex-col gap-1 p-4`}>
            <p className="text-sm text-tertiary">{label}</p>
            <p className="font-display text-display-xs font-semibold text-primary">{value}</p>
        </div>
    );
}

/** What one sprint produced: counts from its history, then its positions. */
export function SprintReportPage() {
    const { id } = useParams();
    const navigate = useNavigate();
    const { sprints, reload } = useSprints();
    const [report, setReport] = useState<SprintReport | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [completing, setCompleting] = useState(false);

    const load = useCallback(async () => {
        setError(null);
        try {
            setReport(await api.get<SprintReport>(`/api/sprints/${id}/report`));
        } catch (e) {
            setError(errorMessage(e));
        }
    }, [id]);

    useEffect(() => {
        load();
    }, [load]);

    const sprint = report?.sprint;

    return (
        <div className="flex w-full flex-col gap-6">
            <Button color="link-gray" size="md" iconLeading={ArrowLeft} href="/sprints" className="self-start">
                All sprints
            </Button>
            {error ? (
                <ErrorState message={error} onRetry={load} />
            ) : !report || !sprint ? (
                <LoadingState>Loading the report...</LoadingState>
            ) : (
                <>
                    <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                        <div className="min-w-0">
                            <p className="flex items-center gap-2 text-sm font-semibold text-brand-secondary">
                                {sprint.name}
                                <Badge type="pill-color" size="sm" color={sprint.status === "active" ? "brand" : "gray"}>
                                    {sprint.status === "active" ? "Running" : "Completed"}
                                </Badge>
                            </p>
                            <h1 className={`${pageTitleClass} mt-1`}>{sprint.goal || "No goal set"}</h1>
                            <p className="mt-1 text-md text-tertiary">
                                {formatDate(sprint.startsOn)} to {formatDate(sprint.endsOn)}
                                {sprint.completedAt && `, completed ${formatDate(sprint.completedAt)}`}
                            </p>
                        </div>
                        {sprint.status === "active" && (
                            <Button size="md" onPress={() => setCompleting(true)}>
                                Complete sprint
                            </Button>
                        )}
                    </header>

                    <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-6">
                        <Tile label="Positions" value={sprint.items} />
                        <Tile label="Applied" value={sprint.applied} />
                        <Tile label="Interviews" value={sprint.interviews} />
                        <Tile label="Offers" value={sprint.offers} />
                        <Tile label="Rejected" value={sprint.rejected} />
                        <Tile label="Average match" value={report.averageScore == null ? "none yet" : `${report.averageScore}%`} />
                    </div>
                    <p className="-mt-3 text-sm text-tertiary">{report.gapsClosed} {report.gapsClosed === 1 ? "gap" : "gaps"} closed across these positions.</p>

                    {report.items.length === 0 ? (
                        <p className={`${cardClass} p-6 text-md text-tertiary`}>No positions were in this sprint.</p>
                    ) : (
                        <ul className={itemsOnPage}>
                            {report.items.map((item) => {
                                const stage = stages.find((s) => s.id === item.stage);
                                return (
                                    <li key={item.applicationId}>
                                        <Link
                                            to={`/positions/${item.postingId}`}
                                            className="flex flex-col gap-1 px-5 py-3 outline-focus-ring transition hover:bg-primary_hover focus-visible:outline-2 md:flex-row md:items-center md:gap-4"
                                        >
                                            <span className="w-16 shrink-0 text-xs font-medium text-quaternary">{item.key}</span>
                                            <span className="min-w-0 flex-1">
                                                <span className="block truncate text-md font-semibold text-primary">{item.title}</span>
                                                <span className="block truncate text-sm text-tertiary">{item.company}</span>
                                            </span>
                                            {stage && (
                                                <Badge type="pill-color" size="sm" color={stage.color}>
                                                    {stage.label}
                                                </Badge>
                                            )}
                                            <span className="text-sm font-semibold text-primary md:w-24 md:text-right">{item.score == null ? "Not checked" : `${item.score}% match`}</span>
                                        </Link>
                                    </li>
                                );
                            })}
                        </ul>
                    )}

                    <CompleteSprintModal
                        sprint={sprint}
                        count={sprints?.length ?? 0}
                        isOpen={completing}
                        onClose={() => setCompleting(false)}
                        onDone={(result) => {
                            setCompleting(false);
                            reload();
                            navigate(`/sprints/${result.completed.id}`);
                            load();
                        }}
                    />
                </>
            )}
        </div>
    );
}
