import { type ReactNode, createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { api } from "@/core/api";

export { ApiError } from "@/core/api";

export type CurrentUser = {
    id: string;
    email: string;
    displayName: string;
    avatarUrl: string | null;
    gitHubLogin: string | null;
};

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
        api.get<CurrentUser>("/api/auth/me")
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
            signedIn(await api.post<CurrentUser>("/api/auth/login", { email, password, rememberMe })),
        [signedIn],
    );

    const register = useCallback(
        async (name: string, email: string, password: string) =>
            signedIn(await api.post<CurrentUser>("/api/auth/register", { name, email, password })),
        [signedIn],
    );

    const logout = useCallback(async () => {
        try {
            await api.post<null>("/api/auth/logout");
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
