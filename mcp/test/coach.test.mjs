import { test } from "node:test";
import assert from "node:assert/strict";
import { CoachClient, setApplicationStage, listPostings } from "../dist/coach.js";

// Fake API: routes map "METHOD /path" to (body, cookie) => [status, json]. Records every call.
function fakeApi(routes) {
  const calls = [];
  let logins = 0;
  const fetchFn = async (url, init) => {
    const path = new URL(url).pathname + new URL(url).search;
    const key = `${init.method} ${path}`;
    const body = init.body ? JSON.parse(init.body) : undefined;
    calls.push({ key, body, cookie: init.headers.Cookie });
    if (key === "POST /api/auth/login") {
      logins++;
      return new Response("{}", {
        status: 200,
        headers: [["Set-Cookie", `.coach=session${logins}; path=/; httponly; samesite=lax`]],
      });
    }
    const handler = routes[key];
    if (!handler) return Response.json({ title: "Not Found" }, { status: 404 });
    const [status, json] = handler(body, init.headers.Cookie);
    return status === 204 ? new Response(null, { status }) : Response.json(json, { status });
  };
  return { fetchFn, calls, logins: () => logins };
}

const client = (api) => new CoachClient("http://api", "lotte@demo.test", "pw", api.fetchFn);
const card = (id, postingId, stage) => ({ id, postingId, company: "Brightlane", title: "Dev intern", city: "Gent", stage });

test("logs in, sends the cookie, and logs in again once on 401", async () => {
  let first = true;
  const api = fakeApi({
    "GET /api/applications": (_b, cookie) => {
      if (first) { first = false; return [401, {}]; }
      return [200, [card("a1", "p1", "applied")]];
    },
  });
  const c = client(api);
  const apps = await c.request("GET", "/api/applications");
  assert.equal(apps.length, 1);
  assert.equal(api.logins(), 2);
  const appCalls = api.calls.filter((x) => x.key === "GET /api/applications");
  assert.equal(appCalls[0].cookie, ".coach=session1");
  assert.equal(appCalls[1].cookie, ".coach=session2");
  assert.deepEqual(api.calls[0].body, { email: "lotte@demo.test", password: "pw", rememberMe: false });
});

test("a failed login surfaces the API problem title", async () => {
  const c = new CoachClient("http://api", "x@y.z", "bad", async () =>
    Response.json({ title: "Email or password is incorrect." }, { status: 401 }));
  await assert.rejects(c.request("GET", "/api/postings"), /Email or password is incorrect/);
});

test("stage move creates the application when the posting is not on the board", async () => {
  const board = [];
  const api = fakeApi({
    "GET /api/postings": () => [200, [{ id: "p1", company: "Brightlane", title: "Dev intern", city: "Gent" }]],
    "GET /api/applications": () => [200, board],
    "POST /api/applications": (b) => { const c = card("a9", b.postingId, "saved"); board.push(c); return [201, c]; },
    "PATCH /api/applications/a9": (b) => [200, { ...board[0], stage: b.stage }],
  });
  const r = await setApplicationStage(client(api), { query: "brightlane", stage: "Interview" });
  assert.match(r.text, /now interview/);
  const keys = api.calls.map((x) => x.key);
  assert.ok(keys.includes("POST /api/applications"));
  assert.deepEqual(api.calls.find((x) => x.key === "PATCH /api/applications/a9").body, { stage: "interview", notes: null });
});

test("ambiguous query returns candidates and changes nothing", async () => {
  const api = fakeApi({
    "GET /api/postings": () => [200, [
      { id: "p1", company: "Brightlane", title: "Backend intern", city: "Gent" },
      { id: "p2", company: "Brightlane", title: "Frontend intern", city: "Antwerpen" },
    ]],
  });
  const r = await setApplicationStage(client(api), { query: "brightlane", stage: "Applied" });
  assert.equal(r.data.ambiguous, true);
  assert.deepEqual(r.data.candidates.map((p) => p.id), ["p1", "p2"]);
  assert.ok(!api.calls.some((x) => x.key.startsWith("POST /api/applications") || x.key.startsWith("PATCH")));
});

test("list_postings joins the board stage onto dashboard postings", async () => {
  const api = fakeApi({
    "GET /api/dashboard": () => [200, { postings: [
      { id: "p1", company: "Brightlane", title: "Dev intern", city: "Gent", language: "NL", score: 62, requirements: [{}] },
      { id: "p2", company: "Acme", title: "QA intern", city: "Leuven", language: "EN", score: 0, requirements: [] },
    ] }],
    "GET /api/applications": () => [200, [card("a1", "p1", "applied")]],
  });
  const r = await listPostings(client(api));
  assert.equal(r.data.postings[0].stage, "applied");
  assert.equal(r.data.postings[1].stage, null);
  assert.equal(r.data.postings[1].score, null);
  assert.match(r.text, /score 62% stage applied/);
});
