#!/usr/bin/env node
// Host-side bridge so the API in Docker can use the host's Claude Code CLI (`claude -p`) as an AI fallback.
// POST /complete {model, system, turns, maxTokens} with `Authorization: Bearer $CLAUDE_BRIDGE_TOKEN`
// -> {text, inputTokens, outputTokens}. One call at a time. Never logs prompts or replies.
//
//   CLAUDE_BRIDGE_TOKEN=... [BRIDGE_HOST=0.0.0.0] [BRIDGE_PORT=8787] node scripts/claude-bridge.mjs
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { timingSafeEqual, createHash } from 'node:crypto';
import { tmpdir } from 'node:os';

const TOKEN = process.env.CLAUDE_BRIDGE_TOKEN ?? '';
const HOST = process.env.BRIDGE_HOST || '127.0.0.1';
const PORT = Number(process.env.BRIDGE_PORT ?? 8787);
const TIMEOUT_MS = Number(process.env.BRIDGE_TIMEOUT_MS ?? 120_000);
const MAX_BODY = 2 * 1024 * 1024;
const MODELS = new Set(['sonnet', 'haiku']);

if (TOKEN.length < 16) {
  console.error('Set CLAUDE_BRIDGE_TOKEN (at least 16 characters).');
  process.exit(1);
}

const digest = (s) => createHash('sha256').update(s).digest();
const authorized = (header) => timingSafeEqual(digest(header ?? ''), digest(`Bearer ${TOKEN}`));

// Same flags as ClaudeCliLlmTransport.Arguments; `--opt=value` so a value starting with '-' is never a flag.
const cliArgs = (model, system) => [
  '-p', '--output-format=json', `--model=${model}`, `--system-prompt=${system}`,
  '--tools=', '--no-session-persistence', '--setting-sources=', '--strict-mcp-config',
];

const prompt = (turns) =>
  turns.length === 1
    ? turns[0].content
    : 'Conversation so far:\n\n' +
      turns.map((t) => `${t.role === 'user' ? 'User' : 'Assistant'}: ${t.content}\n\n`).join('') +
      'Reply as the assistant to the last user message.';

function runClaude({ model, system, turns, maxTokens }) {
  return new Promise((resolve, reject) => {
    const child = spawn('claude', cliArgs(model, system), {
      cwd: tmpdir(),
      env: { ...process.env, CLAUDE_CODE_MAX_OUTPUT_TOKENS: String(maxTokens) },
      stdio: ['pipe', 'pipe', 'ignore'],
    });
    let out = '';
    const timer = setTimeout(() => { child.kill('SIGKILL'); reject(new Error('timeout')); }, TIMEOUT_MS);
    child.stdout.on('data', (d) => { out += d; });
    child.on('error', (e) => { clearTimeout(timer); reject(e); });
    child.on('close', () => {
      clearTimeout(timer);
      try {
        const r = JSON.parse(out);
        if (r.is_error || typeof r.result !== 'string') return reject(new Error('cli error'));
        const u = r.usage ?? {};
        resolve({
          text: r.result,
          inputTokens: (u.input_tokens ?? 0) + (u.cache_creation_input_tokens ?? 0) + (u.cache_read_input_tokens ?? 0),
          outputTokens: u.output_tokens ?? 0,
        });
      } catch { reject(new Error('unreadable cli output')); }
    });
    child.stdin.on('error', () => {});
    child.stdin.end(prompt(turns));
  });
}

function valid(b) {
  return b && MODELS.has(b.model) && typeof b.system === 'string'
    && Array.isArray(b.turns) && b.turns.length > 0
    && b.turns.every((t) => t && (t.role === 'user' || t.role === 'assistant') && typeof t.content === 'string')
    && Number.isInteger(b.maxTokens) && b.maxTokens > 0 && b.maxTokens <= 64_000;
}

let queue = Promise.resolve();
const send = (res, status, body) => {
  res.writeHead(status, { 'content-type': 'application/json' });
  res.end(JSON.stringify(body));
};

const server = createServer((req, res) => {
  if (req.method !== 'POST' || req.url !== '/complete') return send(res, 404, { error: 'not found' });
  if (!authorized(req.headers.authorization)) return send(res, 401, { error: 'unauthorized' });

  let size = 0;
  const chunks = [];
  req.on('data', (c) => {
    size += c.length;
    if (size > MAX_BODY) {
      if (!res.headersSent) send(res, 413, { error: 'body too large' });
      chunks.length = 0; // keep draining (so the client reads the 413) but store nothing
      return;
    }
    chunks.push(c);
  });
  req.on('end', () => {
    if (size > MAX_BODY) return;
    let body;
    try { body = JSON.parse(Buffer.concat(chunks).toString('utf8')); } catch { return send(res, 400, { error: 'invalid json' }); }
    if (!valid(body)) return send(res, 400, { error: 'invalid request' });

    const started = Date.now();
    queue = queue.then(() => runClaude(body)).then(
      (r) => { console.log(`ok model=${body.model} ${Date.now() - started}ms`); send(res, 200, r); },
      (e) => { console.log(`fail model=${body.model} ${e.message}`); send(res, 502, { error: 'claude cli failed' }); },
    );
  });
});

server.listen(PORT, HOST, () => {
  const { address, port } = server.address();
  console.log(`claude-bridge listening on ${address}:${port}`);
});
