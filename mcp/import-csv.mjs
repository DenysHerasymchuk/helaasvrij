// Adds every row of a CSV as a position through the helaasvrij MCP server (propose, then confirm).
// Usage (from mcp/): COACH_TOKEN=hv_... COACH_API_URL=http://localhost:5080 node import-csv.mjs file.csv [--dry]
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";
import fs from "node:fs";

function parseCsv(text) {
  const rows = []; let row = [], cell = "", q = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (q) { if (c === '"' && text[i + 1] === '"') { cell += '"'; i++; } else if (c === '"') q = false; else cell += c; }
    else if (c === '"') q = true;
    else if (c === ",") { row.push(cell); cell = ""; }
    else if (c === "\n" || c === "\r") { if (c === "\r" && text[i + 1] === "\n") i++; row.push(cell); cell = ""; if (row.some((x) => x)) rows.push(row); row = []; }
    else cell += c;
  }
  if (cell || row.length) { row.push(cell); rows.push(row); }
  const [head, ...body] = rows;
  return body.map((r) => Object.fromEntries(head.map((h, i) => [h, (r[i] ?? "").trim()])));
}

const blocked = /(^|\.)(linkedin\.com|lnkd\.in|student\.be|jobteaser\.com)$/;
const known = (v) => v && !/^not stated$/i.test(v) && v !== "-";
// Only the posting's own facts go in; the columns about me (match reasons, skills to highlight, notes) stay out of the posting text.
function describe(r) {
  const line = (label, v) => (known(v) ? `${label}: ${v}\n` : "");
  const text =
    `${r["Internship Title"]} at ${r["Company Name"]}\n\n` +
    line("Location", [r["City"], r["Region"]].filter(known).join(", ")) +
    line("Work arrangement", r["Work Arrangement"]) + line("Category", r["Internship Category"]) +
    line("Technologies", r["Technologies / Tech Stack"]) + line("Compensation", r["Compensation / Salary"]) +
    line("Duration", r["Duration"]) + line("Start date", r["Start Date"]) + line("Required languages", r["Required Languages"]) +
    line("Eligibility", r["Eligibility Requirements"]) + line("Application deadline", r["Application Deadline"]) +
    line("Date posted", r["Date Posted"]) + line("Company website", r["Company Website"]) + line("Vacancy source", r["Original Vacancy Source URL"]);
  const link = r["Direct Application URL"];
  let url;
  try { if (link && !blocked.test(new URL(link).hostname)) url = link; } catch {}
  return { text: text.trim(), url };
}

const [file, flag] = process.argv.slice(2);
const rows = parseCsv(fs.readFileSync(file, "utf8")).filter((r) => r["Internship Title"] && r["Company Name"]);
if (flag === "--dry") { console.log(rows.length, "rows\n" + JSON.stringify(describe(rows[0]), null, 1)); process.exit(0); }

const t = new StdioClientTransport({ command: "node", args: ["dist/index.js"], env: { ...process.env } });
const c = new Client({ name: "csv-import", version: "0" });
await c.connect(t);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
// The API allows 20 chat/tool calls a minute per user; wait and retry on 429.
async function call(name, args) {
  for (let attempt = 0; attempt < 6; attempt++) {
    const r = await c.callTool({ name, arguments: args });
    if (r.isError && /429/.test(r.content[0].text)) { await sleep(20000); continue; }
    return r;
  }
  throw new Error("rate limited");
}
const report = { added: [], existing: [], failed: [] };
for (const [i, row] of rows.entries()) {
  const label = `${row["Company Name"]} - ${row["Internship Title"]}`;
  try {
    const { text, url } = describe(row);
    const p = await call("add_position", url ? { text, url } : { text });
    const proposal = p.structuredContent?.proposal;
    if (p.isError || !proposal) throw new Error(p.content[0].text);
    await sleep(3200);
    const done = await call("confirm_action", { tool: proposal.tool, args: proposal.args });
    if (done.isError) throw new Error(done.content[0].text);
    const msg = done.content[0].text;
    (/already saved/.test(msg) ? report.existing : report.added).push(label);
    console.log(`${i + 1}/${rows.length} ${msg}`);
  } catch (e) {
    report.failed.push(`${label}: ${e.message}`);
    console.log(`${i + 1}/${rows.length} FAILED ${label}: ${e.message}`);
  }
  await sleep(3200);
}
console.log(JSON.stringify({ added: report.added.length, existing: report.existing.length, failed: report.failed }, null, 1));
await c.close();
