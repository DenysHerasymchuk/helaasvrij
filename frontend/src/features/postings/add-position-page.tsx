import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { LoadingState, PageFrame, PageHeader } from "@/core/ui";
import { AddPostingModal } from "./add-posting-modal";

export const BOOKMARKLET_TYPE = "helaasvrij-position";
export const BOOKMARKLET_READY = "helaasvrij-ready";
const MaxText = 20000;

type Incoming = { text: string; url: string };

/** Opened by the bookmarklet from a job page. It only fills the form; nothing is saved until the user confirms. */
export function AddPositionPage() {
    const navigate = useNavigate();
    const [incoming, setIncoming] = useState<Incoming | null>(null);

    useEffect(() => {
        const onMessage = (e: MessageEvent) => {
            const d = e.data as { type?: unknown; title?: unknown; url?: unknown; text?: unknown } | null;
            if (d?.type !== BOOKMARKLET_TYPE || typeof d.text !== "string" || typeof d.url !== "string") return;
            const title = typeof d.title === "string" ? d.title : "";
            setIncoming({ text: `${title}\n\n${d.text}`.trim().slice(0, MaxText), url: d.url.slice(0, 2000) });
        };
        window.addEventListener("message", onMessage);
        // The opener (the job page) waits for this before it sends anything; the target is unknown, the payload is a constant.
        window.opener?.postMessage(BOOKMARKLET_READY, "*");
        return () => window.removeEventListener("message", onMessage);
    }, []);

    return (
        <PageFrame width="max-w-3xl">
            <PageHeader title="Add a position" description="Use the bookmark on a job page and it lands here." />
            {incoming ? (
                <AddPostingModal
                    isOpen
                    initial={incoming}
                    onClose={() => navigate("/backlog")}
                    onAdded={(posting) => navigate(`/positions/${posting.id}`)}
                />
            ) : (
                <LoadingState>
                    {window.opener ? "Waiting for the job page..." : "Open a job page and click the bookmark. You can install it in Settings > Preferences."}
                </LoadingState>
            )}
        </PageFrame>
    );
}
