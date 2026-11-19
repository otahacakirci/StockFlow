// Shared helpers for the agentic guardrail hooks (Claude Code).
// Installed into <repo>/.claude/hooks/agentic/ by the agentic-repo-setup skill.
// Keep behaviour identical to ../python/agentic_lib.py — both are checked against test-vectors.json.
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';

export const POLICY_RELATIVE = '.claude/hooks/agentic/policy.json';

export async function readPayload() {
  let raw = '';
  for await (const chunk of process.stdin) raw += chunk;
  if (!raw.trim()) return {};
  return JSON.parse(raw); // a malformed payload is an error: the caller fails closed
}

export function projectRoot(payload) {
  return toPosix(process.env.CLAUDE_PROJECT_DIR || payload.cwd || process.cwd());
}

export function loadPolicy(root) {
  const file = path.join(root, POLICY_RELATIVE);
  const policy = JSON.parse(readFileSync(file, 'utf8'));
  if (policy.version !== 1) throw new Error(`Unsupported policy version in ${POLICY_RELATIVE}`);
  return policy;
}

// ---------------------------------------------------------------- paths

/** Forward-slash form of a path. Backslashes are separators only on Windows (C:\x, \\server\share, or a win32 host). */
export function toPosix(p) {
  const raw = String(p);
  const windowsLike = process.platform === 'win32' || /^[A-Za-z]:[\\/]/.test(raw) || raw.startsWith('\\\\');
  let s = windowsLike ? raw.replace(/\\/g, '/') : raw;
  if (/^[A-Za-z]:\//.test(s)) s = s[0].toLowerCase() + s.slice(1);
  return s;
}

const isAbs = (s) => s.startsWith('/') || /^[a-z]:\//i.test(s);

/** Repo-relative POSIX path, or null when the path is outside the project. */
export function repoRelative(p, root, cwd) {
  const r = toPosix(root).replace(/\/+$/, '');
  let s = /^[a-z]:\//i.test(r) ? toPosix(String(p).replace(/\\/g, '/')) : toPosix(p); // Windows project: "src\\x" too
  // Git Bash spelling (/c/Users/...) of a Windows drive path
  if (/^[a-z]:\//i.test(r) && /^\/[A-Za-z]\//.test(s)) s = `${s[1].toLowerCase()}:${s.slice(2)}`;
  if (!isAbs(s)) s = `${toPosix(cwd || root).replace(/\/+$/, '')}/${s}`;
  s = path.posix.normalize(s);
  const caseInsensitive = /^[a-z]:\//i.test(r);
  const a = caseInsensitive ? s.toLowerCase() : s;
  const b = caseInsensitive ? r.toLowerCase() : r;
  if (a === b) return '';
  if (a.startsWith(`${b}/`)) return s.slice(r.length + 1);
  return null;
}

export function globToRegExp(glob) {
  let re = '';
  let inBrace = false;
  for (let i = 0; i < glob.length; i += 1) {
    const c = glob[i];
    if (c === '*' && glob[i + 1] === '*') {
      if (glob[i + 2] === '/') {
        re += '(?:.*/)?';
        i += 2;
      } else {
        re += '.*';
        i += 1;
      }
    } else if (c === '*') re += '[^/]*';
    else if (c === '?') re += '[^/]';
    else if (c === '{') {
      inBrace = true;
      re += '(?:';
    } else if (c === '}' && inBrace) {
      inBrace = false;
      re += ')';
    } else if (c === ',' && inBrace) re += '|';
    else re += c.replace(/[.+^$()|[\]\\]/g, '\\$&');
  }
  return new RegExp(`^${re}$`);
}

export const matchesAny = (p, globs = []) => globs.some((g) => globToRegExp(g).test(p));

/** "src/**" → "src/", ".claude/settings.json" → ".claude/settings.json" (for spotting paths in shell text). */
export const literalPrefix = (glob) => glob.split(/[*?{]/)[0];

// ---------------------------------------------------------------- tools

export const EDIT_TOOLS = new Set(['Write', 'Edit', 'MultiEdit', 'NotebookEdit']);
export const SHELL_TOOLS = new Set(['Bash', 'PowerShell']);

export function editedPaths(input = {}) {
  const out = [];
  for (const key of ['file_path', 'notebook_path', 'path']) if (typeof input[key] === 'string') out.push(input[key]);
  if (Array.isArray(input.edits)) for (const e of input.edits) if (typeof e?.file_path === 'string') out.push(e.file_path);
  return out;
}

// ---------------------------------------------------------------- shell parsing

/** Split a command line into segments on && || ; | & and newlines that are outside quotes. */
export function splitTopLevel(command) {
  const parts = [];
  let cur = '';
  let quote = null;
  for (let i = 0; i < command.length; i += 1) {
    const c = command[i];
    if (quote) {
      cur += c;
      if (c === quote && command[i - 1] !== '\\') quote = null;
      continue;
    }
    if (c === '"' || c === "'") {
      quote = c;
      cur += c;
      continue;
    }
    if (c === '\n' || c === ';' || c === '&' || c === '|') {
      // keep redirections like 2>&1 and >& intact
      if (c === '&' && (command[i - 1] === '>' || command[i + 1] === '>')) {
        cur += c;
        continue;
      }
      parts.push(cur);
      cur = '';
      if ((c === '&' || c === '|') && command[i + 1] === c) i += 1;
      continue;
    }
    cur += c;
  }
  parts.push(cur);
  return parts.map((s) => s.trim()).filter(Boolean);
}

/** Inner command strings hidden in $(…), `…`, bash -c "…", pwsh -Command "…", cmd /c …, eval … */
export function nestedCommands(segment) {
  const found = [];
  for (const m of segment.matchAll(/\$\(([^()]*(?:\([^()]*\)[^()]*)*)\)/g)) found.push(m[1]);
  for (const m of segment.matchAll(/`([^`]+)`/g)) found.push(m[1]);
  const tokens = tokenize(segment);
  for (let i = 0; i < tokens.length - 1; i += 1) {
    const t = baseName(tokens[i]).toLowerCase();
    const next = tokens[i + 1].toLowerCase();
    if (['bash', 'sh', 'zsh', 'dash', 'ksh', 'fish'].includes(t) && /^-[a-z]*c[a-z]*$/.test(next) && tokens[i + 2]) found.push(tokens[i + 2]);
    if (['pwsh', 'powershell', 'pwsh.exe', 'powershell.exe'].includes(t)) {
      const j = tokens.findIndex((x, k) => k > i && /^-(c|command)$/i.test(x));
      if (j > 0 && tokens[j + 1]) found.push(tokens.slice(j + 1).join(' '));
    }
    if ((t === 'cmd' || t === 'cmd.exe') && /^\/[ck]$/.test(next)) found.push(tokens.slice(i + 2).join(' '));
    if (t === 'eval') found.push(tokens.slice(i + 1).join(' '));
  }
  return found;
}

/** All segments, including nested ones, flattened. */
export function allSegments(command, depth = 0) {
  const out = [];
  for (const seg of splitTopLevel(command)) {
    out.push(seg);
    if (depth < 4) for (const inner of nestedCommands(seg)) out.push(...allSegments(inner, depth + 1));
  }
  return out;
}

export function tokenize(segment) {
  const tokens = [];
  let cur = '';
  let quote = null;
  let has = false;
  for (let i = 0; i < segment.length; i += 1) {
    const c = segment[i];
    if (quote) {
      if (c === quote) quote = null;
      else cur += c;
      continue;
    }
    if (c === '"' || c === "'") {
      quote = c;
      has = true;
      continue;
    }
    if (/\s/.test(c)) {
      if (has || cur) tokens.push(cur);
      cur = '';
      has = false;
      continue;
    }
    cur += c;
    has = true;
  }
  if (has || cur) tokens.push(cur);
  return tokens;
}

const baseName = (t) => t.replace(/\\/g, '/').split('/').pop();

const WRAPPERS = new Set(['sudo', 'doas', 'env', 'nohup', 'time', 'nice', 'ionice', 'command', 'builtin', 'exec', 'stdbuf', 'noglob']);

/** Tokens of a segment with leading assignments, wrappers and grouping removed; program reduced to its base name. */
export function normalizedTokens(segment) {
  let tokens = tokenize(segment.replace(/^[({\s]+/, '').replace(/[)}\s]+$/, ''));
  for (;;) {
    if (!tokens.length) return tokens;
    if (/^[A-Za-z_][A-Za-z0-9_]*=/.test(tokens[0])) {
      tokens = tokens.slice(1);
      continue;
    }
    const b = baseName(tokens[0]).toLowerCase();
    if (WRAPPERS.has(b)) {
      tokens = tokens.slice(1);
      while (tokens.length && (tokens[0].startsWith('-') || /^[A-Za-z_][A-Za-z0-9_]*=/.test(tokens[0]))) tokens = tokens.slice(1);
      continue;
    }
    if (b === 'timeout') {
      tokens = tokens.slice(1);
      while (tokens.length && tokens[0].startsWith('-')) tokens = tokens.slice(1);
      tokens = tokens.slice(1); // duration
      continue;
    }
    if (b === 'xargs' && tokens[1] && !tokens[1].startsWith('-')) {
      tokens = tokens.slice(1);
      continue;
    }
    break;
  }
  tokens[0] = baseName(tokens[0]).replace(/\.exe$/i, '');
  return tokens;
}

export function currentBranch(cwd) {
  try {
    return execFileSync('git', ['rev-parse', '--abbrev-ref', 'HEAD'], {
      cwd,
      encoding: 'utf8',
      timeout: 4000,
      stdio: ['ignore', 'pipe', 'ignore'],
    }).trim();
  } catch {
    return '';
  }
}

// ---------------------------------------------------------------- write targets

const SECRET_ENV = /^\.env(\..+)?$/i;
const ENV_TEMPLATE = /^\.env\.(example|sample|template|dist|defaults)$/i;

/** .env, .env.local, config/.env.production … but not committed templates such as .env.example. */
export function isSecretEnvPath(p) {
  const base = toPosix(p).split('/').pop();
  return SECRET_ENV.test(base) && !ENV_TEMPLATE.test(base);
}

/** Targets of > >> >| &> n> redirections outside quotes (fd duplications such as 2>&1 are skipped). */
export function redirectTargets(seg) {
  const out = [];
  let quote = null;
  for (let i = 0; i < seg.length; i += 1) {
    const c = seg[i];
    if (quote) {
      if (c === quote) quote = null;
      continue;
    }
    if (c === '"' || c === "'") {
      quote = c;
      continue;
    }
    if (c !== '>') continue;
    let j = i + 1;
    if (seg[j] === '>' || seg[j] === '|') j += 1;
    if (seg[j] === '&') {
      i = j;
      continue;
    }
    while (j < seg.length && /\s/.test(seg[j])) j += 1;
    let target = '';
    let q = null;
    while (j < seg.length) {
      const d = seg[j];
      if (q) {
        if (d === q) q = null;
        else target += d;
      } else if (d === '"' || d === "'") q = d;
      else if (/[\s;&|<>]/.test(d)) break;
      else target += d;
      j += 1;
    }
    if (target) out.push(target);
    i = j - 1;
  }
  return out;
}

const POWERSHELL_WRITERS = new Set(['set-content', 'add-content', 'out-file', 'new-item', 'copy-item', 'move-item', 'sc', 'ac']);

/** Files a shell segment writes to, as far as its text shows: redirections, tee, cp/mv/install/ln, PowerShell writers. */
export function fileWriteTargets(seg, tokens) {
  const targets = redirectTargets(seg);
  const prog = (tokens[0] ?? '').toLowerCase();
  const args = tokens.slice(1);
  const positional = args.filter((a) => !a.startsWith('-'));
  if (prog === 'tee') targets.push(...positional);
  else if (['cp', 'mv', 'install', 'ln'].includes(prog) && positional.length >= 2) targets.push(positional[positional.length - 1]);
  else if (POWERSHELL_WRITERS.has(prog)) {
    const k = args.findIndex((a) => /^-(path|filepath|literalpath|destination)$/i.test(a));
    if (k >= 0 && args[k + 1]) targets.push(args[k + 1]);
    else if (positional.length) targets.push(['copy-item', 'move-item'].includes(prog) ? positional[positional.length - 1] : positional[0]);
  }
  return targets;
}

// ---------------------------------------------------------------- output

export function emitPreToolUse(decision, reason) {
  if (decision === 'pass') process.exit(0);
  process.stdout.write(
    JSON.stringify({
      hookSpecificOutput: { hookEventName: 'PreToolUse', permissionDecision: decision, permissionDecisionReason: reason },
    }),
  );
  process.exit(0);
}

export function failClosed(err) {
  process.stderr.write(`agentic guard failed closed: ${err?.message ?? err}. Fix the hook or ${POLICY_RELATIVE}; do not bypass it.\n`);
  process.exit(2);
}
