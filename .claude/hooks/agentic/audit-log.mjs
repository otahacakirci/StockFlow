#!/usr/bin/env node
// PostToolUse hook (async): append one line per tool call to .claude/audit/tool-calls.jsonl (gitignored).
// A reviewable trail of what an agent actually did, independent of its own summary. Never blocks.
import { appendFileSync, mkdirSync } from 'node:fs';
import path from 'node:path';
import { EDIT_TOOLS, SHELL_TOOLS, editedPaths, projectRoot, readPayload } from './lib.mjs';

const SECRET = /((?:token|secret|password|passwd|api[_-]?key|authorization)\s*[=:]\s*|bearer\s+)\S+/gi;

try {
  const payload = await readPayload();
  const tool = String(payload.tool_name ?? '');
  const input = payload.tool_input ?? {};
  const entry = {
    at: new Date().toISOString(),
    session: payload.session_id ?? null,
    agent: payload.agent_type ?? 'main',
    tool,
    ...(SHELL_TOOLS.has(tool) ? { command: String(input.command ?? '').replace(SECRET, '$1***').slice(0, 500) } : {}),
    ...(EDIT_TOOLS.has(tool) ? { files: editedPaths(input) } : {}),
  };
  const dir = path.join(projectRoot(payload), '.claude', 'audit');
  mkdirSync(dir, { recursive: true });
  appendFileSync(path.join(dir, 'tool-calls.jsonl'), `${JSON.stringify(entry)}\n`);
} catch {
  // Auditing must never break the agent's work.
}
process.exit(0);
