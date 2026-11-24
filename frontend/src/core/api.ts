/** Error from the API: a headline plus optional per-field messages (camelCase field names). */
export class ApiError extends Error {
    status: number;
    fieldErrors: Record<string, string>;

    constructor(status: number, message: string, fieldErrors: Record<string, string> = {}) {
        super(message);
        this.status = status;
        this.fieldErrors = fieldErrors;
    }
}

type Method = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

/** A 401 means the session is gone: send the user to /login and bring them back to this page afterwards. */
function redirectToLogin() {
    const { pathname, search } = window.location;
    if (pathname === "/login" || pathname === "/register") return;
    window.location.assign(`/login?from=${encodeURIComponent(pathname + search)}`);
}

async function request<T>(url: string, method: Method, body?: unknown): Promise<T | null> {
    const hasBody = method === "POST" || method === "PUT" || method === "PATCH" || (method === "DELETE" && body !== undefined);
    const response = await fetch(url, {
        method,
        credentials: "same-origin",
        // The API only accepts JSON on writes (CSRF defence together with SameSite=Lax), so a bodyless POST sends "{}".
        headers: hasBody ? { "Content-Type": "application/json" } : undefined,
        body: hasBody ? JSON.stringify(body ?? {}) : undefined,
    });

    if (response.ok) {
        return response.status === 204 ? null : ((await response.json()) as T);
    }

    // `/api/auth/me` answers 401 for "not signed in": the auth provider handles that itself.
    if (response.status === 401 && url !== "/api/auth/me") redirectToLogin();

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

export const api = {
    get: <T>(url: string) => request<T>(url, "GET"),
    post: <T>(url: string, body?: unknown) => request<T>(url, "POST", body),
    put: <T>(url: string, body?: unknown) => request<T>(url, "PUT", body),
    patch: <T>(url: string, body?: unknown) => request<T>(url, "PATCH", body),
    del: (url: string, body?: unknown) => request<null>(url, "DELETE", body),
};
