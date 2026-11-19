#!/usr/bin/env node
// Guardrails are code: if they silently stop denying, nobody notices. Run this in CI.
//   node .claude/hooks/agentic/test-guard.mjs
// 1. Engine tests: test-vectors.json (fixed fixture policy) — must pass in every repository.
// 2. Project smoke tests: derived from this repository's own policy.json.
// 3. Wiring test: runs guard.mjs as a real process with a malformed payload and expects a fail-closed exit 2.
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { decide } from './guard.mjs';
import { matchesAny, toPosix } from './lib.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const vectorsFile = [path.join(here, 'test-vectors.json'), path.join(here, '..', 'test-vectors.json')].find(existsSync);
const vectors = JSON.parse(readFileSync(vectorsFile, 'utf8'));
let failures = 0;
let count = 0;

function check(name, actual, expected, reason) {
  count += 1;
  if (actual !== expected) {
    failures += 1;
    console.error(`FAIL ${name}: expected ${expected}, got ${actual}${reason ? ` (${reason})` : ''}`);
  }
}

for (const c of vectors.cases) {
  const root = c.root ?? '/repo';
  const payload = { cwd: root, ...c.payload };
  const { decision, reason } = decide(payload, vectors.policy, { root: toPosix(root), branch: () => c.branch ?? 'feature/x' });
  check(c.name, decision, c.expect, reason);
}

const policyFile = path.join(here, 'policy.json');
if (existsSync(policyFile)) {
  const policy = JSON.parse(readFileSync(policyFile, 'utf8'));
  const root = '/repo';
  const run = (payload, branch = 'agent/smoke') => decide({ cwd: root, ...payload }, policy, { root, branch: () => branch }).decision;
  for (const b of policy.protectedBranches ?? []) {
    check(`project: push to ${b} is denied`, run({ tool_name: 'Bash', tool_input: { command: `git push origin ${b}` } }), 'deny');
  }
  for (const g of policy.guardrailPaths ?? []) {
    const sample = g.replace(/\*\*\/?/g, 'x/').replace(/\*/g, 'x').replace(/\/$/, '/x');
    check(`project: subagent cannot edit guardrail ${g}`, run({ tool_name: 'Edit', agent_id: 's', agent_type: '__smoke__', tool_input: { file_path: `${root}/${sample}` } }), 'deny');
  }
  for (const [name, p] of Object.entries(policy.agents ?? {})) {
    const sub = { agent_id: 's', agent_type: name };
    for (const g of p.write ?? []) {
      const sample = g.replace(/\{([^,}]+)[^}]*\}/g, '$1').replace(/\*\*\/?/g, 'x/').replace(/\*/g, 'x').replace(/\/$/, '/x');
      const protectedSample = matchesAny(sample, policy.protectedPaths) || matchesAny(sample, policy.guardrailPaths) || matchesAny(sample, policy.libraryPaths);
      if (!protectedSample) check(`project: ${name} may edit ${sample}`, run({ tool_name: 'Edit', ...sub, tool_input: { file_path: `${root}/${sample}` } }), 'pass');
    }
    if (!p.library) {
      check(`project: ${name} cannot edit CLAUDE.md`, run({ tool_name: 'Edit', ...sub, tool_input: { file_path: `${root}/CLAUDE.md` } }), 'deny');
    }
    if (p.shell === 'readonly') {
      check(`project: ${name} cannot post PR comments`, run({ tool_name: 'Bash', ...sub, tool_input: { command: 'gh pr comment 1 --body x' } }), 'deny');
    }
  }
}

const proc = spawnSync(process.execPath, [path.join(here, 'guard.mjs')], { input: '{not json', encoding: 'utf8' });
check('wiring: malformed payload fails closed with exit 2', String(proc.status), '2', proc.stderr.trim());

if (failures) {
  console.error(`\n${failures} of ${count} guard checks failed.`);
  process.exit(1);
}
console.log(`All ${count} guard checks passed.`);
