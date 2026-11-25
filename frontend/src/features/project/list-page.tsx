import { useMemo, useState } from "react";
import { Link } from "react-router";
import { SearchLg } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Input } from "@/components/base/input/input";
import { ErrorState, LoadingState, cardClass } from "@/core/ui";
import { NativeSelect } from "@/features/postings/fields";
import { stages } from "@/features/applications/types";
import { formatDate } from "@/features/postings/types";
import { type Position, useProject } from "./use-project";

const statusOf = (p: Position) => p.application?.stage ?? "backlog";

export function StatusBadge({ position }: { position: Position }) {
    const stage = stages.find((s) => s.id === position.application?.stage);
    return (
        <Badge type="pill-color" size="sm" color={stage?.color ?? "gray"}>
            {stage?.label ?? "Backlog"}
        </Badge>
    );
}

const th = "whitespace-nowrap px-4 py-3 text-left text-sm font-semibold text-tertiary";
const td = "whitespace-nowrap px-4 py-3 text-sm text-secondary";

/** Every position in one table, like Jira's list view. */
export function ListPage() {
    const { positions, error, reload } = useProject();
    const [query, setQuery] = useState("");
    const [status, setStatus] = useState("all");

    const shown = useMemo(() => {
        const q = query.trim().toLowerCase();
        return (positions ?? [])
            .filter((p) => (status === "all" || statusOf(p) === status) && (!q || `${p.key} ${p.title} ${p.company} ${p.city}`.toLowerCase().includes(q)))
            .sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    }, [positions, query, status]);

    if (error) return <ErrorState message={error} onRetry={reload} />;
    if (!positions) return <LoadingState>Loading positions...</LoadingState>;

    return (
        <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
                <div className="sm:w-72">
                    <Input aria-label="Search positions" icon={SearchLg} placeholder="Search key, title, company or city" value={query} onChange={setQuery} />
                </div>
                <div className="sm:w-48">
                    <NativeSelect aria-label="Filter by status" value={status} onChange={(e) => setStatus(e.target.value)}>
                        <option value="all">All statuses</option>
                        <option value="backlog">Backlog</option>
                        {stages.map((s) => (
                            <option key={s.id} value={s.id}>
                                {s.label}
                            </option>
                        ))}
                    </NativeSelect>
                </div>
            </div>

            <div className={`${cardClass} overflow-hidden`}>
                <div className="overflow-x-auto">
                    <table className="w-full min-w-[760px] border-collapse">
                        <thead className="border-b border-secondary bg-secondary">
                            <tr>
                                <th scope="col" className={th}>Key</th>
                                <th scope="col" className={th}>Position</th>
                                <th scope="col" className={th}>Company</th>
                                <th scope="col" className={th}>Status</th>
                                <th scope="col" className={th}>Sprint</th>
                                <th scope="col" className={th}>Match</th>
                                <th scope="col" className={th}>Deadline</th>
                                <th scope="col" className={th}>Added</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-secondary">
                            {shown.map((p) => (
                                <tr key={p.id} className="hover:bg-primary_hover">
                                    <td className={`${td} font-medium text-quaternary`}>{p.key}</td>
                                    <td className={`${td} max-w-72 truncate`}>
                                        <Link to={`/positions/${p.id}`} className="font-semibold text-primary outline-focus-ring hover:underline focus-visible:outline-2">
                                            {p.title}
                                        </Link>
                                    </td>
                                    <td className={td}>{p.company}</td>
                                    <td className={td}>
                                        <StatusBadge position={p} />
                                    </td>
                                    <td className={td}>{p.sprint?.name ?? "None"}</td>
                                    <td className={td}>{p.analysed && p.score != null ? `${p.score}%` : "Not checked"}</td>
                                    <td className={td}>{p.deadline ? formatDate(p.deadline) : "None"}</td>
                                    <td className={td}>{formatDate(p.createdAt)}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
                {shown.length === 0 && <p className="px-5 py-6 text-md text-tertiary">{positions.length === 0 ? "No positions yet. Add one from the backlog." : "No positions match."}</p>}
                <p className="border-t border-secondary px-4 py-3 text-sm text-tertiary">
                    {shown.length} of {positions.length}
                </p>
            </div>
        </div>
    );
}
