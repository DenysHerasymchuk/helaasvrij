import { Button } from "@/components/base/buttons/button";
import { SprintBand } from "@/features/sprints/sprint-band";
import { useSprints } from "@/features/sprints/use-sprints";

/** The running sprint with its progress, or a pointer to start one. Renders nothing while loading or when the load failed. */
export function ActiveSprintCard() {
    const { sprints, active } = useSprints();
    if (sprints === null) return null;

    if (!active) {
        return (
            <section className="flex flex-col items-start gap-3 rounded-xl bg-primary p-5 shadow-xs ring-1 ring-secondary md:p-6">
                <h2 className="text-md font-semibold text-primary">{sprints.length === 0 ? "Start your first sprint" : "No sprint is running"}</h2>
                <p className="text-sm text-tertiary">A sprint is one round of applications with a goal and an end date. Pull positions in from your backlog and track them on the board.</p>
                <Button size="md" href="/backlog">
                    Open backlog
                </Button>
            </section>
        );
    }

    return (
        <SprintBand
            sprint={active}
            action={
                <Button size="md" href="/board">
                    Open board
                </Button>
            }
        />
    );
}
