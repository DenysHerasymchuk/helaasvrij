import { useEffect, useState } from "react";
import { Link } from "react-router";
import { Plus } from "@untitledui/icons";
import { Badge } from "@/components/base/badges/badges";
import { Button } from "@/components/base/buttons/button";
import { ApiError, api } from "@/core/api";
import { type Application, stageLabel, stages } from "./types";

/**
 * The board keyed by posting id; null while loading. A failed load leaves it empty, so the add button shows:
 * adding is idempotent on the server, so a posting already on the board just comes back with its stage.
 */
export function useBoard() {
    const [byPosting, setByPosting] = useState<Map<string, Application> | null>(null);

    useEffect(() => {
        api.get<Application[]>("/api/applications")
            .then((list) => setByPosting(new Map((list ?? []).map((a) => [a.postingId, a]))))
            .catch(() => setByPosting(new Map()));
    }, []);

    const added = (application: Application) => setByPosting((m) => new Map(m ?? []).set(application.postingId, application));
    return { byPosting, added };
}

type BoardStatusProps = {
    postingId: string;
    byPosting: Map<string, Application> | null;
    onAdded: (application: Application) => void;
};

/** "Add to applications" for a posting, or where it sits on the board once it is there. */
export function BoardStatus({ postingId, byPosting, onAdded }: BoardStatusProps) {
    const [adding, setAdding] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const application = byPosting?.get(postingId);

    if (byPosting === null) return null;

    if (application) {
        const color = stages.find((s) => s.id === application.stage)?.color ?? "gray";
        return (
            <div className="flex flex-wrap items-center gap-2">
                <Badge type="pill-color" size="md" color={color}>
                    On your board: {stageLabel(application.stage)}
                </Badge>
                <Link to="/applications" className="rounded-xs text-sm font-semibold text-brand-secondary outline-focus-ring hover:underline focus-visible:outline-2 focus-visible:outline-offset-2">
                    Open board
                </Link>
            </div>
        );
    }

    async function add() {
        setAdding(true);
        setError(null);
        try {
            const created = await api.post<Application>("/api/applications", { postingId });
            if (created) onAdded(created);
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Could not add this posting. Try again.");
        } finally {
            setAdding(false);
        }
    }

    return (
        <div className="flex flex-col items-start gap-1">
            <Button size="sm" color="secondary" iconLeading={Plus} onPress={add} isLoading={adding} isDisabled={adding}>
                Add to applications
            </Button>
            {error && (
                <p role="alert" className="text-sm text-error-primary">
                    {error}
                </p>
            )}
        </div>
    );
}
