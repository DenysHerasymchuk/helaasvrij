// node --test scripts/claude-bridge.test.mjs
// Spawns the bridge with a fake `claude` binary on PATH; no real CLI or network.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, chmodSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname, delimiter } from 'node:path';
import { fileURLToPath } from 'node:url';

const TOKEN = 'test-token-0123456789';
let bridge;
let baseUrl;
let binDir;

before(async () => {
  binDir = mkdtempSync(join(tmpdir(), 'fake-claude-'));
  const fake = join(binDir, 'claude');
  // Echoes the args and stdin back so the test can check what the bridge passed.
  writeFileSync(fake, `#!/usr/bin/env node
let s = '';
process.stdin.on('data', (d) => (s += d)).on('end', () => {
  process.stdout.write(JSON.stringify({ type: 'result', is_error: false,
    result: JSON.stringify({ args: process.argv.slice(2), stdin: s }),
    usage: { input_tokens: 3, cache_creation_input_tokens: 4, cache_read_input_tokens: 0, output_tokens: 5 } }));
});
`);
  chmodSync(fake, 0o755);

  const script = join(dirname(fileURLToPath(import.meta.url)), 'claude-bridge.mjs');
  bridge = spawn(process.execPath, [script], {
    env: { ...process.env, PATH: `${binDir}${delimiter}${process.env.PATH}`, CLAUDE_BRIDGE_TOKEN: TOKEN, BRIDGE_PORT: '0', BRIDGE_HOST: '' },
    stdio: ['ignore', 'pipe', 'inherit'],
  });
  baseUrl = await new Promise((resolve, reject) => {
    bridge.stdout.on('data', (d) => {
      const m = /listening on ([\d.]+):(\d+)/.exec(String(d));
      if (m) resolve(`http://${m[1]}:${m[2]}`);
    });
    bridge.on('exit', (code) => reject(new Error(`bridge exited ${code}`)));
  });
});

after(() => {
  bridge?.kill();
  rmSync(binDir, { recursive: true, force: true });
});

const body = { model: 'haiku', system: 'be terse', turns: [{ role: 'user', content: 'Reply with OK' }], maxTokens: 100 };
const post = (headers, payload = body) =>
  fetch(`${baseUrl}/complete`, { method: 'POST', headers: { 'content-type': 'application/json', ...headers }, body: JSON.stringify(payload) });

test('binds to 127.0.0.1 by default', () => {
  assert.match(baseUrl, /^http:\/\/127\.0\.0\.1:/);
});

test('rejects a missing or wrong token', async () => {
  assert.equal((await post({})).status, 401);
  assert.equal((await post({ authorization: 'Bearer wrong' })).status, 401);
});

test('rejects an unknown model', async () => {
  assert.equal((await post({ authorization: `Bearer ${TOKEN}` }, { ...body, model: '--dangerously-skip-permissions' })).status, 400);
});

test('rejects an oversized body', async () => {
  const big = { ...body, system: 'x'.repeat(3 * 1024 * 1024) };
  assert.equal((await post({ authorization: `Bearer ${TOKEN}` }, big)).status, 413);
});

test('runs claude -p without tools and returns text and usage', async () => {
  const res = await post({ authorization: `Bearer ${TOKEN}` });
  assert.equal(res.status, 200);
  const json = await res.json();
  assert.equal(json.inputTokens, 7);
  assert.equal(json.outputTokens, 5);
  const echoed = JSON.parse(json.text);
  assert.equal(echoed.stdin, 'Reply with OK');
  assert.ok(echoed.args.includes('-p'));
  assert.ok(echoed.args.includes('--model=haiku'));
  assert.ok(echoed.args.includes('--tools='));
  assert.ok(echoed.args.includes('--system-prompt=be terse'));
});
