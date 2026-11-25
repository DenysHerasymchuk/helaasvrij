import { useCallback, useEffect, useState } from "react";
import { Button } from "@/components/base/buttons/button";
import { Input } from "@/components/base/input/input";
import { ApiError, api } from "@/core/api";
import { itemsInCard, metaClass } from "@/core/ui";
import { SettingsCard } from "./settings-row";

type Token = { id: string; name: string; hint: string; createdAt: string; lastUsedAt: string | null; revoked: boolean };
type Created = { token: Token; secret: string };

const day = (iso: string) => new Date(iso).toLocaleDateString("en-BE", { day: "numeric", month: "short", year: "numeric" });

/** Personal access tokens for tools such as the MCP server. The secret is shown once, right after it is made. */
export function TokensCard() {
    const [tokens, setTokens] = useState<Token[] | null>(null);
    const [name, setName] = useState("");
    const [fresh, setFresh] = useState<string | null>(null);
    const [copied, setCopied] = useState(false);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => setTokens((await api.get<Token[]>("/api/tokens")) ?? []), []);
    useEffect(() => {
        void load();
    }, [load]);

    async function create() {
        setBusy(true);
        setError(null);
        try {
            const made = await api.post<Created>("/api/tokens", { name });
            setFresh(made!.secret);
            setCopied(false);
            setName("");
            await load();
        } catch (e) {
            setError(e instanceof ApiError ? Object.values(e.fieldErrors)[0] ?? e.message : "Something went wrong. Try again.");
        } finally {
            setBusy(false);
        }
    }

    async function revoke(id: string) {
        await api.del(`/api/tokens/${id}`);
        await load();
    }

    const active = (tokens ?? []).filter((t) => !t.revoked);

    return (
        <SettingsCard title="Access tokens" description="Let a tool such as the Claude MCP server use your account without your password. A token can read and change your positions, and you can revoke it any time.">
            <form
                className="flex flex-wrap items-end gap-3 p-4"
                onSubmit={(e) => {
                    e.preventDefault();
                    if (name.trim() && !busy) void create();
                }}
            >
                <div className="min-w-48 flex-1">
                    <Input label="Name" size="md" placeholder="Claude Code on my laptop" value={name} onChange={setName} />
                </div>
                <Button size="md" type="submit" isLoading={busy} isDisabled={!name.trim()}>
                    Create token
                </Button>
            </form>
            {error && (
                <p role="alert" className="px-4 pb-3 text-sm text-error-primary">
                    {error}
                </p>
            )}
            {fresh && (
                <div className="mx-4 mb-4 flex flex-col gap-2 rounded-lg bg-secondary p-3 ring-1 ring-secondary">
                    <p className="text-sm font-semibold text-primary">Copy it now. It is not shown again.</p>
                    <code className="break-all text-sm text-secondary">{fresh}</code>
                    <div>
                        <Button
                            size="sm"
                            color="secondary"
                            onPress={() => {
                                void navigator.clipboard.writeText(fresh);
                                setCopied(true);
                            }}
                        >
                            {copied ? "Copied" : "Copy"}
                        </Button>
                    </div>
                </div>
            )}
            {tokens && active.length === 0 ? (
                <p className={`${metaClass} px-4 pb-4`}>No tokens yet.</p>
            ) : (
                <ul className={itemsInCard}>
                    {active.map((t) => (
                        <li key={t.id} className="flex items-center justify-between gap-3 bg-primary px-3 py-2">
                            <div className="min-w-0">
                                <p className="truncate text-sm font-semibold text-primary">{t.name}</p>
                                <p className={metaClass}>
                                    ending {t.hint}, made {day(t.createdAt)}, {t.lastUsedAt ? `last used ${day(t.lastUsedAt)}` : "never used"}
                                </p>
                            </div>
                            <Button size="sm" color="secondary-destructive" onPress={() => void revoke(t.id)}>
                                Revoke
                            </Button>
                        </li>
                    ))}
                </ul>
            )}
        </SettingsCard>
    );
}
