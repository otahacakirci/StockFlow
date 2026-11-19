#!/usr/bin/env node
// PreToolUse guardrail for every agent in this repository (Claude Code).
// Instructions in CLAUDE.md and .claude/rules *guide* agents; this hook *enforces* the rules that must never be
// broken, using the per-repository policy in .claude/hooks/agentic/policy.json.
//
// Decisions: "deny" (never allowed), "ask" (a human must approve in the permission dialog), "pass" (no opinion —
// the normal permission flow continues). Any internal error exits 2, which blocks the tool call (fail closed).
// Same behaviour as ../python/guard.py; both are verified against test-vectors.json.
import { pathToFileURL } from 'node:url';
import {
  EDIT_TOOLS,
  SHELL_TOOLS,
  allSegments,
  currentBranch,
  editedPaths,
  emitPreToolUse,
  failClosed,
  fileWriteTargets,
  isSecretEnvPath,
  literalPrefix,
  loadPolicy,
  matchesAny,
  normalizedTokens,
  projectRoot,
  readPayload,
  repoRelative,
} from './lib.mjs';

const RANK = { pass: 0, ask: 1, deny: 2 };

const READONLY_PREFIXES = [
  'git status', 'git diff', 'git log', 'git show', 'git blame', 'git ls-files', 'git rev-parse', 'git merge-base',
  'git branch --show-current', 'git branch --list', 'git branch -a', 'git branch -r', 'git fetch', 'git remote -v',
  'git cat-file', 'git describe', 'git shortlog', 'git grep',
  'gh pr view', 'gh pr diff', 'gh pr checks', 'gh pr list', 'gh pr status', 'gh issue view', 'gh issue list',
  'gh run view', 'gh run list', 'gh repo view',
  'glab mr view', 'glab mr diff', 'glab mr list', 'glab issue view', 'glab issue list',
  'cat', 'head', 'tail', 'less', 'ls', 'dir', 'tree', 'wc', 'grep', 'egrep', 'rg', 'pwd', 'echo', 'which', 'file',
  'stat', 'du', 'sort', 'uniq', 'cut', 'jq', 'diff', 'true', 'test', 'basename', 'dirname', 'realpath', 'printf',
  'get-content', 'get-childitem', 'select-string', 'get-location', 'test-path', 'write-output',
  'cd', 'pushd', 'popd', 'set-location', 'find',
];

/** True when the segment writes a file through > or >> outside quotes (ignoring 2>&1 and >/dev/null). */
export function hasRedirect(seg) {
  const s = seg.replace(/\d?>&\d/g, '').replace(/\d?>\s*(\/dev\/null|nul)\b/gi, '');
  let quote = null;
  for (let i = 0; i < s.length; i += 1) {
    const c = s[i];
    if (quote) {
      if (c === quote) quote = null;
    } else if (c === '"' || c === "'") quote = c;
    else if (c === '>' && s[i - 1] !== '-' && s[i - 1] !== '=') return true;
  }
  return false;
}

/** MCP tool names whose action (after the last "__") starts with one of these verbs change remote state. */
export const WRITE_VERB =
  /^(create|update|delete|merge|push|write|add|post|submit|approve|close|reopen|assign|unassign|request|dismiss|edit|remove|set|fork|rerun|cancel|run|trigger|lock|unlock|transfer|upload|publish|send|label|resolve)(_|$)/;

/** find actions that delete, run commands or write files — not allowed for read-only roles. */
const FIND_WRITERS = ['-delete', '-exec', '-execdir', '-ok', '-okdir', '-fprint', '-fprint0', '-fprintf', '-fls'];

const DELETE_PROGRAMS = new Set(['rm', 'rmdir', 'unlink', 'shred', 'del', 'erase', 'rd', 'remove-item', 'ri', 'trash']);

export function decide(payload, policy, ctx) {
  const tool = String(payload.tool_name ?? '');
  const input = payload.tool_input ?? {};
  const isSubagent = Boolean(payload.agent_id);
  const role = payload.agent_type ? String(payload.agent_type) : 'main';
  const listed = Object.prototype.hasOwnProperty.call(policy.agents ?? {}, role) ? policy.agents[role] : null;
  const profile =
    listed ?? (isSubagent && policy.unlistedSubagents === 'readonly' ? { write: [], shell: 'readonly', mcp: 'readonly' } : null);
  const ci = /^[a-z]:\//i.test(ctx.root);
  const match = (p, globs = []) => matchesAny(ci ? p.toLowerCase() : p, ci ? globs.map((g) => g.toLowerCase()) : globs);
  const who = isSubagent ? `subagent "${role}"` : listed ? `agent "${role}"` : 'the main session';

  const results = [];
  const add = (decision, reason) => results.push({ decision, reason });

  if (EDIT_TOOLS.has(tool)) checkEdits();
  else if (SHELL_TOOLS.has(tool)) checkShell(String(input.command ?? ''));
  else if (tool.startsWith('mcp__')) checkMcp();

  results.sort((a, b) => RANK[b.decision] - RANK[a.decision]);
  return results[0] ?? { decision: 'pass', reason: '' };

  // ------------------------------------------------------------------ edits
  function checkEdits() {
    for (const raw of editedPaths(input)) {
      const rel = repoRelative(raw, ctx.root, payload.cwd);
      if (rel === null) {
        if (isSubagent || profile) add('deny', `${raw} is outside the repository; ${who} may only edit files inside it.`);
        continue;
      }
      if (match(rel, policy.protectedPaths)) {
        add('deny', `${rel} is protected (generated, vendored or secret). Change it through the tool that produces it, or ask a human.`);
        continue;
      }
      if (match(rel, policy.guardrailPaths)) {
        if (isSubagent) add('deny', `${rel} is a guardrail. Subagents never change guardrails; a human changes them in a reviewed pull request.`);
        else add('ask', `${rel} is a guardrail (hooks, policy, settings or code owners). A human must approve this change, and it should land through a reviewed pull request.`);
        continue;
      }
      if (match(rel, policy.approvalPaths)) {
        if (isSubagent) add('deny', `${rel} needs human approval to change (dependencies, environment, CI or another sensitive file). Report the needed change instead.`);
        else add('ask', `${rel} needs human approval to change (dependencies, environment, CI or another sensitive file).`);
        continue;
      }
      if (match(rel, policy.libraryPaths)) {
        if (profile?.library) continue;
        if (isSubagent || profile) add('deny', `${rel} is part of the shared agent library. Only the knowledge-curator drafts library changes, and a human approves them.`);
        else add('ask', `${rel} is part of the shared agent library (CLAUDE.md, rules, skills, agents). A human must approve library changes.`);
        continue;
      }
      if (profile && !match(rel, profile.write ?? [])) {
        const owner = ownerOf(rel);
        add(
          'deny',
          `${who} may only edit ${(profile.write ?? []).join(', ') || 'nothing'}. ${rel} is outside its scope — ` +
            `report it as a dependency${owner ? ` for "${owner}"` : ''} instead of changing it.`,
        );
      }
    }
  }

  function ownerOf(rel) {
    for (const [name, p] of Object.entries(policy.agents ?? {})) if (name !== role && match(rel, p.write ?? [])) return name;
    return '';
  }

  // ------------------------------------------------------------------ shell
  function checkShell(command) {
    if (!command.trim()) return;
    const shellMode = profile?.shell ?? 'full';
    if (shellMode === 'none') {
      add('deny', `${who} has no shell access by policy.`);
      return;
    }

    // Whole-command patterns that only make sense across a pipeline.
    if (/\b(curl|wget|iwr|irm|invoke-webrequest|invoke-restmethod)\b[^\n]*\|\s*(sudo\s+)?(sh|bash|zsh|python3?|node|iex|invoke-expression)\b/i.test(command)) {
      add('deny', 'Piping downloaded content into an interpreter is never allowed.');
    }
    checkShellTouchesGuardrails(command);

    for (const seg of allSegments(command)) {
      const tokens = normalizedTokens(seg);
      if (!tokens.length) continue;
      const prog = tokens[0].toLowerCase();
      for (const target of fileWriteTargets(seg, tokens)) {
        if (isSecretEnvPath(target)) add('deny', `Agents do not write secrets files (${target}).`);
      }
      for (const rule of policy.shell?.deny ?? []) if (new RegExp(rule.pattern, 'i').test(seg)) add('deny', rule.reason);
      for (const rule of policy.shell?.ask ?? []) if (new RegExp(rule.pattern, 'i').test(seg)) add('ask', rule.reason);
      if (prog === 'git') checkGit(tokens.slice(1));
      else if (prog === 'gh') checkGh(tokens.slice(1));
      else if (prog === 'glab') checkGlab(tokens.slice(1));
      else if (DELETE_PROGRAMS.has(prog)) add('ask', `Deleting files needs explicit human consent ("${seg.slice(0, 120)}").`);
      else if (prog === 'find' && tokens.some((t) => t === '-delete' || ((t === '-exec' || t === '-execdir') && /rm|del/.test(tokens.join(' '))))) {
        add('ask', 'find … -delete removes files and needs explicit human consent.');
      }
      if (shellMode === 'readonly') checkReadonly(seg, tokens);
    }
  }

  function checkShellTouchesGuardrails(command) {
    const writeish = /(>|\btee\b|\bsed\s+-i|\bperl\s+-i|\bmv\b|\bcp\b|\brm\b|\bchmod\b|\btruncate\b|\bgit\s+(checkout|restore|rm|mv)\b|set-content|out-file|copy-item|move-item|remove-item)/i;
    if (!writeish.test(command.replace(/\d?>&\d/g, '').replace(/\d?>\s*(\/dev\/null|nul)\b/gi, ''))) return;
    const lower = command.replace(/\\/g, '/').toLowerCase();
    for (const g of policy.guardrailPaths ?? []) {
      const lit = literalPrefix(g).toLowerCase();
      if (lit && lower.includes(lit)) {
        if (isSubagent) add('deny', `This command appears to modify a guardrail (${g}). Subagents never change guardrails.`);
        else add('ask', `This command appears to modify a guardrail (${g}). A human must approve it.`);
        return;
      }
    }
  }

  function checkReadonly(seg, tokens) {
    if (hasRedirect(seg)) {
      add('deny', `${who} is read-only: output redirection writes files. Print to stdout instead.`);
      return;
    }
    const line = tokens.join(' ').toLowerCase();
    const extra = (policy.shell?.readonlyCommands ?? []).concat(profile?.readonlyCommands ?? []).map((c) => c.toLowerCase());
    const ok = [...READONLY_PREFIXES, ...extra].some((p) => line === p || line.startsWith(`${p} `));
    if (tokens[0] === 'gh' && tokens[1] === 'api' && ghApiIsRead(tokens.slice(2))) return;
    if (tokens[0] === 'find' && tokens.some((t) => FIND_WRITERS.includes(t))) {
      add('deny', `${who} is read-only: find with -delete, -exec/-ok or -fprint/-fls is not allowed.`);
      return;
    }
    if (!ok) add('deny', `${who} is read-only. "${seg.slice(0, 120)}" is not on its read-only command list.`);
  }

  function ghApiIsRead(args) {
    for (let i = 0; i < args.length; i += 1) {
      const t = args[i];
      if ((t === '-X' || t === '--method') && String(args[i + 1]).toUpperCase() !== 'GET') return false;
      if (/^--method=/.test(t) && t.split('=')[1].toUpperCase() !== 'GET') return false;
      if (/^-X[A-Za-z]+$/.test(t) && t.slice(2).toUpperCase() !== 'GET') return false;
      if (/^(-f|-F|--field|--raw-field|--input)(=|$)/.test(t)) return false;
    }
    return true;
  }

  // ------------------------------------------------------------------ git
  function checkGit(args) {
    // skip global options: -C <dir>, -c <k=v>, --git-dir=…, --no-pager, …
    let i = 0;
    while (i < args.length && args[i].startsWith('-')) {
      if (args[i] === '-C' || args[i] === '-c') i += 2;
      else i += 1;
    }
    const sub = args[i];
    const rest = args.slice(i + 1);
    const protectedBranches = policy.protectedBranches ?? [];
    const branch = () => ctx.branch();
    const has = (...flags) => rest.some((a) => flags.includes(a) || flags.some((f) => f.endsWith('=') && a.startsWith(f)));
    if (args.slice(0, i).some((a, k) => args[k - 1] === '-c' && /^core\.hookspath=/i.test(a))) {
      add('deny', 'Overriding core.hooksPath disables the repository git hooks.');
    }

    switch (sub) {
      case 'push': {
        if (has('--no-verify')) add('deny', 'git push --no-verify skips the repository git hooks.');
        if (has('--mirror', '--all')) add('deny', 'git push --mirror/--all can overwrite protected branches. Push one feature branch.');
        if (has('--force', '-f', '--force-with-lease', '--force-with-lease=', '--force-if-includes')) {
          add('deny', 'Force-pushing rewrites shared history. Push a new commit instead.');
        }
        const positional = rest.filter((a) => !a.startsWith('-'));
        const refspecs = positional.slice(1);
        const deleting = has('--delete', '-d');
        if (refspecs.some((r) => r.startsWith('+'))) add('deny', 'A "+refspec" force-pushes. Push a new commit instead.');
        const targets = refspecs.length ? refspecs.map(dst) : [branch()];
        const hitsProtected = targets.filter((t) => t && protectedBranches.includes(t));
        if (hitsProtected.length) {
          add('deny', `Agents never push to ${hitsProtected.join(', ')}. Push a feature branch and open a pull request; a human merges.`);
        } else if (deleting || refspecs.some((r) => r.startsWith(':'))) {
          add('ask', 'Deleting a remote branch needs explicit human consent.');
        } else {
          add('ask', 'Pushing publishes commits outside this machine. A human approves each push.');
        }
        break;
      }
      case 'commit': {
        if (has('--no-verify', '-n')) add('deny', 'git commit --no-verify skips the repository git hooks.');
        if (policy.denyCommitOnProtectedBranch && protectedBranches.includes(branch())) {
          add('deny', `You are on ${branch()}. Agents never commit to a protected branch: create a feature branch first (git switch -c <type>/<slug>).`);
        }
        if (has('--amend')) add('ask', 'git commit --amend rewrites the last commit. A human approves history rewrites.');
        break;
      }
      case 'reset':
        if (has('--hard')) add('ask', 'git reset --hard discards work irreversibly. A human must approve it.');
        break;
      case 'clean':
        if (rest.some((a) => /^-[a-zA-Z]*f/.test(a) || a === '--force')) add('ask', 'git clean deletes untracked files. A human must approve it.');
        break;
      case 'checkout':
        if (rest.includes('--') || rest.includes('.') || has('-f', '--force')) add('ask', 'This checkout discards uncommitted changes. A human must approve it.');
        break;
      case 'restore':
        if (!(has('--staged', '-S') && !has('--worktree', '-W'))) add('ask', 'git restore discards uncommitted changes. A human must approve it.');
        break;
      case 'switch':
        if (has('--discard-changes', '-f', '--force')) add('ask', 'This switch discards uncommitted changes. A human must approve it.');
        break;
      case 'rm':
        if (!has('--cached')) add('ask', 'git rm deletes tracked files and needs explicit human consent.');
        break;
      case 'add':
        if (has('-A', '--all', '-u', '--update')) add('ask', 'git add -A/-u also stages deletions. Stage explicit paths, or get human consent.');
        break;
      case 'stash':
        if (rest[0] === 'drop' || rest[0] === 'clear') add('ask', 'Dropping stashes loses work. A human must approve it.');
        break;
      case 'branch': {
        if (has('-D', '-d', '--delete')) {
          const names = rest.filter((a) => !a.startsWith('-'));
          if (names.some((n) => protectedBranches.includes(n))) add('deny', `Agents never delete ${names.join(', ')}.`);
          else add('ask', 'Deleting a branch needs explicit human consent.');
        }
        if (has('-f', '--force', '-M', '-C')) add('ask', 'Force-moving or renaming a branch rewrites refs. A human must approve it.');
        break;
      }
      case 'config':
        if (rest.some((a) => /^core\.hookspath$/i.test(a))) add('deny', 'Changing core.hooksPath disables the repository git hooks.');
        break;
      case 'filter-branch':
      case 'filter-repo':
      case 'replace':
        add('ask', 'Rewriting repository history needs explicit human consent.');
        break;
      case 'update-ref':
        if (has('-d')) add('ask', 'Deleting a ref needs explicit human consent.');
        break;
      case 'reflog':
        if (rest[0] === 'expire' || rest[0] === 'delete') add('ask', 'Expiring the reflog removes recovery points. A human must approve it.');
        break;
      case 'gc':
        if (rest.some((a) => /^--prune(=now)?$/.test(a))) add('ask', 'git gc --prune removes unreachable objects permanently. A human must approve it.');
        break;
      case 'tag':
        if (has('-d', '--delete', '-f', '--force')) add('ask', 'Deleting or moving a tag needs explicit human consent.');
        break;
      default:
        break;
    }
  }

  /** Destination branch of a push refspec ("+src:dst", "HEAD", "refs/heads/x"). */
  function dst(refspec) {
    let r = refspec.replace(/^\+/, '');
    if (r.includes(':')) r = r.split(':').pop();
    if (r === 'HEAD' || r === '@') r = ctx.branch();
    return r.replace(/^refs\/heads\//, '');
  }

  // ------------------------------------------------------------------ hosting CLIs
  function checkGh(args) {
    const [group, action] = args;
    const rest = args.slice(2);
    const key = `${group} ${action}`;
    if (key === 'pr merge') add('deny', 'Agents never merge pull requests. A human merges after review.');
    else if (key === 'pr review' && rest.some((a) => a === '--approve' || a === '-a')) add('deny', 'Agents never approve pull requests. A human approves.');
    else if (['pr review', 'pr comment', 'pr close', 'pr reopen', 'pr edit', 'pr lock'].includes(key)) {
      add('ask', 'Posting to a pull request speaks for the team. Agents draft; a human approves what gets posted.');
    } else if (['issue create', 'issue comment', 'issue close', 'issue reopen', 'issue edit', 'issue delete', 'issue transfer', 'issue lock'].includes(key)) {
      add('ask', 'Changing issues is visible to the whole team. A human approves it.');
    } else if (['repo delete', 'repo archive', 'repo rename', 'repo edit'].includes(key) || group === 'secret' || group === 'ruleset') {
      add('deny', 'Repository administration, secrets and rulesets are human-only.');
    } else if (group === 'release' && ['create', 'delete', 'edit', 'upload', 'delete-asset'].includes(action)) {
      add('deny', 'Publishing releases is human-only.');
    } else if (group === 'variable' && ['set', 'delete'].includes(action)) {
      add('deny', 'Changing repository variables is human-only.');
    } else if ((group === 'workflow' && ['run', 'enable', 'disable'].includes(action)) || (group === 'run' && ['rerun', 'cancel', 'delete'].includes(action))) {
      add('ask', 'Triggering or changing CI runs needs human approval.');
    } else if (group === 'api' && !ghApiIsRead(args.slice(1))) {
      add('ask', 'A write call to the hosting API needs human approval.');
    }
  }

  function checkGlab(args) {
    const key = `${args[0]} ${args[1]}`;
    if (key === 'mr merge' || key === 'mr approve') add('deny', 'Agents never merge or approve merge requests. A human does.');
    else if (['mr note', 'mr comment', 'mr close', 'mr update', 'issue create', 'issue note', 'issue close', 'issue update'].includes(key)) {
      add('ask', 'Posting to the team tracker needs human approval.');
    } else if (args[0] === 'release' && args[1] !== 'list' && args[1] !== 'view') add('deny', 'Publishing releases is human-only.');
  }

  // ------------------------------------------------------------------ MCP
  function checkMcp() {
    const name = tool.toLowerCase();
    if (/merge_pull_request|merge_merge_request|approve/.test(name)) add('deny', 'Agents never merge or approve. A human does.');
    const readonly = profile && (profile.mcp === 'readonly' || profile.shell === 'readonly' || profile.shell === 'none');
    const verb = name.split('__').pop();
    if (readonly && WRITE_VERB.test(verb)) {
      add('deny', `${who} is read-only; ${tool} changes remote state.`);
    }
  }
}

async function main() {
  try {
    const payload = await readPayload();
    const root = projectRoot(payload);
    const policy = loadPolicy(root);
    let cached;
    const ctx = {
      root,
      branch: () => {
        if (cached === undefined) cached = currentBranch(payload.cwd || root);
        return cached;
      },
    };
    const { decision, reason } = decide(payload, policy, ctx);
    emitPreToolUse(decision, `[agentic guard] ${reason}`);
  } catch (err) {
    failClosed(err);
  }
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) await main();
