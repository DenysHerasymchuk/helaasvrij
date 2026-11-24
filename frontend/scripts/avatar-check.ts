// Self-check: node --experimental-strip-types scripts/avatar-check.ts
import assert from "node:assert/strict";
import { generateAvatarUrl, hashName } from "../src/core/avatar.ts";

assert.equal(generateAvatarUrl("Alice"), generateAvatarUrl("Alice"));
assert.equal(generateAvatarUrl("  ALICE "), generateAvatarUrl("alice"));
assert.equal(hashName("Lotte Péeters"), hashName("Lotte Péeters")); // decomposed vs composed é
assert.equal(generateAvatarUrl(""), generateAvatarUrl(undefined));
assert.ok(!generateAvatarUrl("Lotte Peeters").toLowerCase().includes("lotte"));
const names = ["Alice", "Bob", "Lotte Peeters", "Denys", "Jan Janssens", "Sofie", "Mohammed", "Emma"];
const urls = new Set(names.map((n) => generateAvatarUrl(n)));
assert.ok(urls.size >= names.length - 1, "names should look different");
for (const n of names) console.log(n.padEnd(14), generateAvatarUrl(n, 96));
console.log("ok");
