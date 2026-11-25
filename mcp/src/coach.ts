// API client and tool logic. Kept free of MCP server types so tests can drive it with a fake fetch.
// The tool list is not written here: it comes from GET /api/tools, the same catalog the in-app chat uses.
import { z } from "zod";

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function toError(res: Response): Promise<ApiError> {
  let title = "";
  try {
    const body = await res.json();
    title = body.title ?? body.message ?? "";
    if (body.errors) title += " " + Object.values(body.errors).flat().join(" ");
  } catch {
    // body was not JSON
  }
  return new ApiError(res.status, `${res.status}: ${title.trim() || res.statusText || "request failed"}`);
}

export type ToolParam = { name: string; description: string; required: boolean; values: string[] | null };
export type ToolInfo = { name: string; description: string; kind: "read" | "action"; params: ToolParam[] };
export type Proposal = { tool: string; args: Record<string, string>; summary: string };
export type Result = { text: string; data: Record<string, unknown> };

export class CoachClient {
  constructor(
    private base: string,
    private token: string | undefined,
    private fetchFn: typeof fetch = fetch,
  ) {}

  async request<T = any>(method: string, path: string, body?: unknown): Promise<T> {
    if (!this.token) throw new ApiError(0, "Set COACH_TOKEN in the MCP server config. Create one in the app under Settings > Account.");
    const res = await this.fetchFn(`${this.base}${path}`, {
      method,
      headers: { Authorization: `Bearer ${this.token}`, ...(body === undefined ? {} : { "Content-Type": "application/json" }) },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (res.status === 401) throw new ApiError(401, "401: the token is unknown or revoked. Create a new one in Settings > Account.");
    if (!res.ok) throw await toError(res);
    return res.status === 204 ? (undefined as T) : res.json();
  }

  catalog = () => this.request<ToolInfo[]>("GET", "/api/tools");
}

/** Postings, notes and ads are user or third party text: mark them so the model treats them as data. */
export const untrusted = (name: string, text: string) =>
  `<untrusted_data name="${name.replace(/[^\w-]/g, "")}">\n${text.replace(/<(\/?)untrusted_data/gi, "<$1untrusted-data")}\n</untrusted_data>`;

/** A read tool returns data. An action tool returns a proposal: nothing has changed until confirm_action runs. */
export async function runTool(c: CoachClient, tool: ToolInfo, args: Record<string, unknown>): Promise<Result> {
  const clean = Object.fromEntries(Object.entries(args).filter(([, v]) => v !== undefined && v !== null).map(([k, v]) => [k, String(v)]));
  const res = await c.request("POST", `/api/tools/${encodeURIComponent(tool.name)}/run`, { args: clean });
  if (res.kind === "proposal") {
    const p = res.pending as Proposal;
    return {
      text:
        `Proposed, nothing has changed yet: ${p.summary}\n` +
        `Ask the user to confirm. Only after they say yes, call confirm_action with tool "${p.tool}" and args ${JSON.stringify(p.args)}.`,
      data: { proposal: p },
    };
  }
  return { text: `${res.summary}\n${untrusted(`tool_${tool.name}`, JSON.stringify(res.data))}`, data: { result: res.data } };
}

export async function confirmAction(c: CoachClient, tool: string, args: Record<string, string>): Promise<Result> {
  const res = await c.request("POST", "/api/chat/actions/confirm", { tool, args });
  return { text: res.message, data: { message: res.message, navigateTo: res.navigateTo ?? null } };
}

/** Zod input shape for one tool: every argument is a string, a fixed word list becomes an enum. */
export function inputShape(tool: ToolInfo): Record<string, z.ZodTypeAny> {
  return Object.fromEntries(
    tool.params.map((p) => {
      const base = p.values?.length ? z.enum(p.values as [string, ...string[]]) : z.string();
      return [p.name, (p.required ? base : base.optional()).describe(p.description)];
    }),
  );
}
