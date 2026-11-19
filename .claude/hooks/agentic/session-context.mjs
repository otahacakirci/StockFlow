#!/usr/bin/env node
// SessionStart hook: live repository state that static instructions cannot know.
// Prints a short context block; never blocks the session (errors degrade to a warning line).
import { execFileSync } from 'node:child_process';
import { existsSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { loadPolicy, projectRoot, readPayload } from './lib.mjs';

function git(cwd, args) {
  try {
    return execFileSync('git', args, { cwd, encoding: 'utf8', timeout: 4000, stdio: ['ignore', 'pipe', 'ignore'] }).trim();
  } catch {
    return '';
  }
}

const lines = ['Session context (from .claude/hooks/agentic/session-context.mjs):'];
try {
  const payload = await readPayload().catch(() => ({}));
  const root = projectRoot(payload);
  let policy = null;
  try {
    policy = loadPolicy(root);
  } catch (err) {
    lines.push(`- WARNING: guardrail policy could not be loaded (${err.message}). Edits and shell commands will be blocked until a human fixes it.`);
  }
  const branch = git(root, ['rev-parse', '--abbrev-ref', 'HEAD']);
  if (!branch) lines.push('- Not a git repository (or no commits yet).');
  else {
    const dirty = git(root, ['status', '--porcelain']).split('\n').filter(Boolean).length;
    lines.push(`- Current branch: ${branch}${dirty ? ` — ${dirty} uncommitted change(s)` : ''}`);
    if (policy?.protectedBranches?.includes(branch)) {
      lines.push(`- ${branch} is protected: create a feature branch before your first edit. Changes reach ${branch} only through a reviewed pull request.`);
    }
  }
  if (policy?.handoffDir) {
    const dir = path.join(root, policy.handoffDir);
    const open = existsSync(dir) ? readdirSync(dir).filter((f) => f.endsWith('.md') && f.toLowerCase() !== 'readme.md') : [];
    lines.push(open.length ? `- Open handoff artifacts: ${open.map((f) => `${policy.handoffDir}/${f}`).join(', ')}` : `- No open handoff artifacts in ${policy.handoffDir}/.`);
  }
  if (policy?.verifyCommand) lines.push(`- Done gate: run \`${policy.verifyCommand}\` and report the result before claiming a change is finished.`);
  else if (policy) lines.push('- No verify command is defined yet. Say so explicitly instead of claiming a change is verified.');
} catch (err) {
  lines.push(`- (session context unavailable: ${err.message})`);
}

process.stdout.write(JSON.stringify({ hookSpecificOutput: { hookEventName: 'SessionStart', additionalContext: lines.join('\n') } }));
