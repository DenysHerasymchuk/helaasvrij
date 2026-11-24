// API client and tool logic. Kept free of MCP types so tests can drive it with a fake fetch.

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function toError(res: Response): Promise<ApiError> {
  let title = "";
  try {
    const body = await res.json();
    title = body.title ?? "";
    if (body.errors) title += " " + Object.values(body.errors).flat().join(" ");
  } catch {
    // body was not JSON
  }
  return new ApiError(res.status, `${res.status}: ${title.trim() || res.statusText || "request failed"}`);
}

export class CoachClient {
  private cookie?: string;

  constructor(
    private base: string,
    private email: string | undefined,
    private password: string | undefined,
    private fetchFn: typeof fetch = fetch,
  ) {}

  private async login(): Promise<void> {
    if (!this.email || !this.password) throw new ApiError(0, "Set COACH_EMAIL and COACH_PASSWORD in the MCP server config.");
    const res = await this.fetchFn(`${this.base}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: this.email, password: this.password, rememberMe: false }),
    });
    if (!res.ok) throw new ApiError(res.status, `Login failed: ${(await toError(res)).message}`);
    // Keep only name=value of each cookie; attributes are for browsers.
    this.cookie = res.headers.getSetCookie().map((c) => c.split(";")[0]).join("; ");
  }

  async request<T = any>(method: string, path: string, body?: unknown): Promise<T> {
    if (!this.cookie) await this.login();
    const send = () =>
      this.fetchFn(`${this.base}${path}`, {
        method,
        headers: { Cookie: this.cookie!, ...(body === undefined ? {} : { "Content-Type": "application/json" }) },
        body: body === undefined ? undefined : JSON.stringify(body),
      });
    let res = await send();
    if (res.status === 401) {
      await this.login();
      res = await send();
    }
    if (!res.ok) throw await toError(res);
    return res.status === 204 ? (undefined as T) : res.json();
  }
}

export type Result = { text: string; data: Record<string, unknown> };

const pct = (n: number | null | undefined) => (n == null ? "-" : `${n}%`);

export async function listPostings(c: CoachClient): Promise<Result> {
  const [dash, apps] = await Promise.all([c.request("GET", "/api/dashboard"), c.request("GET", "/api/applications")]);
  const stageOf = new Map<string, any>(apps.map((a: any) => [a.postingId, a]));
  const postings = dash.postings.map((p: any) => ({
    id: p.id,
    company: p.company,
    title: p.title,
    city: p.city,
    language: p.language,
    score: p.requirements.length ? p.score : null,
    deadline: p.deadline,
    stage: stageOf.get(p.id)?.stage ?? null,
    applicationId: stageOf.get(p.id)?.id ?? null,
  }));
  const text = postings.length
    ? postings
        .map((p: any) => `- ${p.company}: ${p.title} (${p.city}) score ${pct(p.score)} stage ${p.stage ?? "not on board"} [id ${p.id}]`)
        .join("\n")
    : "No saved postings yet.";
  return { text, data: { postings } };
}

export async function searchJobs(c: CoachClient, what?: string, where?: string): Promise<Result> {
  const q = new URLSearchParams();
  if (what) q.set("what", what);
  if (where) q.set("where", where);
  const jobs = (await c.request("GET", `/api/jobs?${q}`)).map((j: any) => ({
    id: j.id,
    title: j.title,
    company: j.company,
    location: j.location,
    created: j.created,
    url: j.url,
  }));
  const text = jobs.length
    ? jobs.map((j: any) => `- ${j.title} at ${j.company ?? "?"} (${j.location ?? "?"}) [jobId ${j.id}]`).join("\n")
    : "No jobs match. Try fewer words or no city.";
  return { text, data: { jobs } };
}

function postingResult(p: any, verb: string): Result {
  return {
    text: `${verb} ${p.company}: ${p.title} (${p.city}), ${p.requirements.length} requirements [id ${p.id}]`,
    data: { id: p.id, company: p.company, title: p.title, city: p.city, requirements: p.requirements.length },
  };
}

export async function saveJob(c: CoachClient, jobId: string): Promise<Result> {
  return postingResult(await c.request("POST", "/api/postings/from-job", { jobId }), "Saved");
}

export async function addPosting(c: CoachClient, text?: string, url?: string): Promise<Result> {
  if (!text && !url) throw new ApiError(0, "Give the posting text or a URL.");
  const p = text
    ? await c.request("POST", "/api/postings/paste", { text, url: url ?? null })
    : await c.request("POST", "/api/postings/url", { url });
  return postingResult(p, "Added");
}

export async function listApplications(c: CoachClient): Promise<Result> {
  const apps = await c.request<any[]>("GET", "/api/applications");
  const text = apps.length
    ? apps.map((a) => `- [${a.stage}] ${a.company}: ${a.title} (${a.city}) [applicationId ${a.id}, postingId ${a.postingId}]`).join("\n")
    : "The board is empty.";
  return { text, data: { applications: apps } };
}

export type StageInput = {
  postingId?: string;
  applicationId?: string;
  query?: string;
  stage: string;
  notes?: string;
};

function cardResult(a: any): Result {
  return { text: `${a.company}: ${a.title} is now ${a.stage}.`, data: { application: a } };
}

export async function setApplicationStage(c: CoachClient, input: StageInput): Promise<Result> {
  const body = { stage: input.stage.toLowerCase(), notes: input.notes ?? null };
  if (input.applicationId) return cardResult(await c.request("PATCH", `/api/applications/${input.applicationId}`, body));

  let postingId = input.postingId;
  if (!postingId) {
    if (!input.query?.trim()) throw new ApiError(0, "Give postingId, applicationId or a query (company or title).");
    const postings = await c.request<any[]>("GET", "/api/postings");
    const words = input.query.toLowerCase().split(/\s+/).filter(Boolean);
    const hits = postings.filter((p) => words.every((w) => `${p.company} ${p.title}`.toLowerCase().includes(w)));
    if (hits.length === 0) throw new ApiError(0, `No saved posting matches "${input.query}". Use list_postings to see them.`);
    if (hits.length > 1) {
      const candidates = hits.map((p) => ({ id: p.id, company: p.company, title: p.title, city: p.city }));
      return {
        text: `"${input.query}" matches ${hits.length} postings. Nothing changed. Pick one by postingId:\n` +
          candidates.map((p) => `- ${p.company}: ${p.title} (${p.city}) [id ${p.id}]`).join("\n"),
        data: { ambiguous: true, candidates },
      };
    }
    postingId = hits[0].id;
  }

  // POST is idempotent: it returns the existing card when the posting is already on the board.
  const card = await c.request("POST", "/api/applications", { postingId });
  return cardResult(await c.request("PATCH", `/api/applications/${card.id}`, body));
}

export async function getPosting(c: CoachClient, postingId: string): Promise<Result> {
  const p = await c.request("GET", `/api/postings/${postingId}`);
  // Evidence quotes come from the student's CV or repos: return the source label only.
  const requirements = p.requirements.map((r: any) => ({
    id: r.id,
    text: r.text,
    importance: r.importance,
    coverage: r.coverage,
    evidenceSource: r.evidenceSource?.label ?? null,
    action: r.action,
    actionProject: r.actionProject,
    dueDate: r.dueDate,
    done: r.done,
  }));
  const a = p.analysis;
  const head = `${p.company}: ${p.title} (${p.city}, ${p.language})${p.url ? ` ${p.url}` : ""}\n` +
    (a.analysed ? `Match score ${pct(a.score)}${a.noClearFit ? " (no clear fit)" : ""}` : "Not analysed yet (open it in the app to run the gap analysis).");
  const lines = requirements.map((r: any) =>
    `- [${r.importance}, ${r.coverage}${r.done ? ", done" : ""}] ${r.text}${r.action ? `\n    action: ${r.action}` : ""}`);
  return {
    text: [head, ...lines].join("\n"),
    data: {
      id: p.id, company: p.company, title: p.title, city: p.city, language: p.language, url: p.url,
      deadline: p.deadline, interviewAt: p.interviewAt, analysed: a.analysed, score: a.analysed ? a.score : null,
      noClearFit: a.noClearFit, requirements,
    },
  };
}

export async function listGaps(c: CoachClient): Promise<Result> {
  const gaps = await c.request<any[]>("GET", "/api/gaps");
  const open = gaps.filter((g) => !g.done);
  const text = open.length
    ? open.map((g) =>
        `- ${g.company} (${pct(g.postingScore)}, +${g.scoreGain} if closed): ${g.requirement} [${g.importance}, ${g.coverage}]` +
        (g.action ? `\n    action: ${g.action}` : "")).join("\n")
    : "No open gaps.";
  return { text, data: { gaps } };
}
