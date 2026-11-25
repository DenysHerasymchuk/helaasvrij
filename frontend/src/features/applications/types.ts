export type Stage = "saved" | "applied" | "interview" | "offer" | "rejected";

export type Application = {
    id: string;
    postingId: string;
    company: string;
    title: string;
    city: string;
    deadline: string | null;
    stage: Stage;
    appliedAt: string | null;
    stageChangedAt: string;
    notes: string;
    createdAt: string;
    sprintId: string | null;
    key: string;
};

export const stages: { id: Stage; label: string; hint: string; color: "gray" | "brand" | "warning" | "success" | "error" }[] = [
    { id: "saved", label: "Saved", hint: "Not sent yet", color: "gray" },
    { id: "applied", label: "Applied", hint: "Waiting for an answer", color: "brand" },
    { id: "interview", label: "Interview", hint: "They want to talk", color: "warning" },
    { id: "offer", label: "Offer", hint: "You got a yes", color: "success" },
    { id: "rejected", label: "Rejected", hint: "Not this time", color: "error" },
];

export const stageLabel = (stage: Stage) => stages.find((s) => s.id === stage)?.label ?? stage;
