import { type ReactNode, createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";

export type CurrentUser = {
    id: string;
    email: string;
    displayName: string;
    avatarUrl: string | null;
    gitHubLogin: string | null;
};

/** Error from the auth API: a headline plus optional per-field messages (camelCase field names). */
export class ApiError extends Error {
    status: number;
    fieldErrors: Record<string, string>;

    constructor(status: number, message: string, fieldErrors: Record<string, string> = {}) {
        super(message);
        this.status = status;
        this.fieldErrors = fieldErrors;
    }
}

async function request<T>(url: string, init?: { method: string; body?: unknown }): Promise<T | null> {
    const response = await fetch(url, {
        method: init?.method ?? "GET",
        credentials: "same-origin",
        // The API only accepts JSON on POST (CSRF defence together with SameSite=Lax), so logout sends "{}" too.
        headers: init?.method === "POST" ? { "Content-Type": "application/json" } : undefined,
        body: init?.method === "POST" ? JSON.stringify(init.body ?? {}) : undefined,
    });

    if (response.ok) {
        return response.status === 204 ? null : ((await response.json()) as T);
    }

    let title = response.status === 429 ? "Too many attempts. Try again in a minute." : "Something went wrong. Try again.";
    const fieldErrors: Record<string, string> = {};
    try {
        const problem = await response.json();
        if (response.status !== 429 && typeof problem.title === "string") title = problem.title;
        for (const [field, messages] of Object.entries(problem.errors ?? {})) {
            if (Array.isArray(messages) && messages.length > 0) fieldErrors[field] = String(messages[0]);
        }
    } catch {
        // Not JSON (proxy error page): keep the generic message.
    }
    throw new ApiError(response.status, title, fieldErrors);
}

type Status = "loading" | "authenticated" | "anonymous";

type AuthContextValue = {
    user: CurrentUser | null;
    status: Status;
    login: (email: string, password: string, rememberMe: boolean) => Promise<void>;
    register: (name: string, email: string, password: string) => Promise<void>;
    logout: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUser] = useState<CurrentUser | null>(null);
    const [status, setStatus] = useState<Status>("loading");

    // Load the session once on start-up. A 401 simply means "not signed in".
    useEffect(() => {
        let cancelled = false;
        request<CurrentUser>("/api/auth/me")
            .then((me) => {
                if (cancelled) return;
                setUser(me);
                setStatus(me ? "authenticated" : "anonymous");
            })
            .catch(() => {
                if (cancelled) return;
                setUser(null);
                setStatus("anonymous");
            });
        return () => {
            cancelled = true;
        };
    }, []);

    const signedIn = useCallback((me: CurrentUser | null) => {
        setUser(me);
        setStatus(me ? "authenticated" : "anonymous");
    }, []);

    const login = useCallback(
        async (email: string, password: string, rememberMe: boolean) =>
            signedIn(await request<CurrentUser>("/api/auth/login", { method: "POST", body: { email, password, rememberMe } })),
        [signedIn],
    );

    const register = useCallback(
        async (name: string, email: string, password: string) =>
            signedIn(await request<CurrentUser>("/api/auth/register", { method: "POST", body: { name, email, password } })),
        [signedIn],
    );

    const logout = useCallback(async () => {
        try {
            await request<null>("/api/auth/logout", { method: "POST" });
        } finally {
            signedIn(null);
        }
    }, [signedIn]);

    const value = useMemo(() => ({ user, status, login, register, logout }), [user, status, login, register, logout]);
    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
    const context = useContext(AuthContext);
    if (!context) throw new Error("useAuth must be used inside <AuthProvider>");
    return context;
}

export const initialsOf = (name: string) =>
    name
        .split(/\s+/)
        .filter(Boolean)
        .slice(0, 2)
        .map((part) => part[0]?.toUpperCase())
        .join("") || "?";

/** Only same-site paths, so a crafted `from` state can never send the user elsewhere. */
export const safePath = (path: unknown) => (typeof path === "string" && path.startsWith("/") && !path.startsWith("//") ? path : "/dashboard");
