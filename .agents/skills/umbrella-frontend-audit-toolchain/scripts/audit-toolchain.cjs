'use strict';

// Read-only, dependency-free inventory. Never require/evaluate a project's config.
const fs = require('node:fs');
const path = require('node:path');
const excluded = new Set(['node_modules', '.git', '.vs', '.ai-shared', '.agents', '.claude', '.github',
  '.codex', 'bin', 'obj', 'wwwroot', 'dist', 'coverage', 'artifacts', 'TestResults', '.worktrees']);
const knownRules = new Set(Array.from({ length: 14 }, (_, i) => `FE${String(i + 1).padStart(3, '0')}`));
const slash = value => value.split(path.sep).join('/');
const relative = (root, value) => slash(path.relative(root, value)) || '.';
const safeSpec = value => typeof value === 'string' && /^[0-9vVxX*^~<>=| .+-]+$/.test(value)
  ? value : '[non-version specification]';

function readJson(file, root) {
  try { return JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '')); }
  catch { throw new Error(`${relative(root, file)}: unreadable or invalid JSON`); }
}

function readConfig(root) {
  const configPath = path.join(root, 'umbrella-frontend.json');
  if (!fs.existsSync(configPath)) return null;
  const config = readJson(configPath, root);
  if (!config || typeof config !== 'object' || Array.isArray(config) || config.baselineVersion !== 1)
    throw new Error('umbrella-frontend.json: unsupported or missing baselineVersion');
  if (config.exceptions !== undefined && !Array.isArray(config.exceptions))
    throw new Error('umbrella-frontend.json: exceptions must be an array');
  for (const item of config.exceptions || []) {
    if (!item || !knownRules.has(item.rule) || typeof item.path !== 'string' || !item.path ||
      item.path.startsWith('/') || /[\\:]/.test(item.path) || item.path.split('/').includes('..') ||
      typeof item.reason !== 'string' || !item.reason.trim())
      throw new Error('umbrella-frontend.json: invalid exception rule, relative path or reason');
  }
  return config;
}

function workspaceMatches(pattern, packageRelative) {
  // Conservative subset. Unsupported glob syntax is reported, never executed.
  if (typeof pattern !== 'string' || /[!\[\]{}?\\]/.test(pattern)) return false;
  const escaped = pattern.replace(/[.+^$()|]/g, '\\$&')
    .replace(/\*\*/g, '\u0000').replace(/\*/g, '[^/]*').replace(/\u0000/g, '.*');
  return new RegExp(`^${escaped.replace(/\/$/, '')}$`).test(packageRelative);
}

function workspacePatterns(json) {
  if (!Object.prototype.hasOwnProperty.call(json, 'workspaces')) return [];
  const declared = json.workspaces;
  const patterns = Array.isArray(declared) ? declared
    : declared && typeof declared === 'object' && Object.keys(declared).every(key => key === 'packages')
      ? declared.packages : null;
  if (!Array.isArray(patterns) || patterns.some(pattern => typeof pattern !== 'string' || !pattern.trim() ||
    /[!\[\]{}?\\:]/.test(pattern) || pattern.startsWith('/') || pattern.split('/').includes('..'))) return null;
  return patterns;
}

function audit(rootInput) {
  const root = fs.realpathSync(rootInput);
  if (!fs.statSync(root).isDirectory()) throw new Error('Repository root must be a directory');
  const config = readConfig(root);
  const report = {
    baselineVersion: 1, repositories: [root], profile: config?.profile ?? 'undiscovered',
    projects: [], findings: [], exceptions: [], changedFiles: [], checks: [],
    limitations: [
      'Static subset of FE001/FE002/FE003/FE008 only; complete the semantic audit in SKILL.md.',
      'No project code, installs, registry queries, builds or watcher tests were executed.',
      'Engine compatibility, dynamic configs, browser support and publish/isolation require separate verification.',
      'Config validation covers baseline and exceptions only; validate the full config against config.schema.json before Apply.',
      'Excluded dependency, generated, agent and .github directories; symlinks are not followed.'
    ]
  };
  const files = [];
  function walk(directory) {
    let entries;
    try { entries = fs.readdirSync(directory, { withFileTypes: true }); }
    catch { report.findings.push({ rule: null, classification: 'unknown', path: relative(root, directory), evidence: 'Directory could not be read.' }); return; }
    for (const entry of entries.sort((a, b) => a.name.localeCompare(b.name))) {
      if (entry.isSymbolicLink()) continue;
      const file = path.join(directory, entry.name);
      if (entry.isDirectory()) { if (!excluded.has(entry.name)) walk(file); }
      else if (entry.isFile()) files.push(file);
    }
  }
  walk(root);
  const packages = [];
  for (const file of files.filter(file => path.basename(file) === 'package.json')) {
    try {
      const json = readJson(file, root);
      if (!json || typeof json !== 'object' || Array.isArray(json)) throw new Error('Manifest must be an object');
      packages.push({ file, directory: path.dirname(file), json, workspacePatterns: workspacePatterns(json) });
    } catch {
      report.findings.push({ rule: null, classification: 'unknown', path: relative(root, file), evidence: 'Package manifest is unreadable or invalid.' });
    }
  }
  function add(rule, file, evidence, recommendation) {
    const findingPath = relative(root, file);
    const exception = (config?.exceptions || []).find(x => x.rule === rule && (x.path === '*' || x.path === findingPath));
    report.findings.push({ rule, classification: exception ? 'intentional-exception' : 'drift', path: findingPath,
      evidence, recommendation, ...(exception ? { reason: exception.reason } : {}) });
  }
  for (const pkg of packages) {
    const { file, directory, json } = pkg;
    if (pkg.workspacePatterns === null)
      report.findings.push({ rule: 'FE003', classification: 'unknown', path: relative(root, file), evidence: 'Malformed or unsupported workspace declaration: inspect lock ownership manually.' });
    let owner = pkg;
    const ancestors = packages.filter(parent => {
      const rel = relative(parent.directory, directory);
      return rel !== '.' && !rel.startsWith('../') && !path.isAbsolute(rel);
    }).sort((a, b) => b.directory.length - a.directory.length);
    const unresolvedAncestors = ancestors.filter(parent => parent.workspacePatterns === null);
    const ownershipUnknown = unresolvedAncestors.length > 0;
    const parents = ancestors.filter(parent => parent.workspacePatterns?.some(pattern =>
      workspaceMatches(pattern, relative(parent.directory, directory))));
    if (parents.length) owner = parents[0];
    // An unsupported ancestor pattern may include this package, even when another
    // ancestor has a known match. Do not invent standalone ownership or inheritance.
    if (ownershipUnknown) {
      for (const rule of ['FE003', 'FE001'])
        report.findings.push({ rule, classification: 'unknown', path: relative(root, file),
          evidence: 'An ancestor has an unsupported workspace declaration; lock ownership and effective engine policy cannot be established.',
          recommendation: 'Resolve ancestor workspace membership before assessing package locks or engine declarations.',
          ancestors: unresolvedAncestors.map(parent => relative(root, parent.file)) });
    }
    const lockFile = ['npm-shrinkwrap.json', 'package-lock.json'].map(name => path.join(owner.directory, name)).find(fs.existsSync);
    const lockDirectories = new Set([directory, owner.directory]);
    // Without an NPM owner lock, an ancestor may own a pnpm/Yarn workspace.
    // Do not treat that as an instruction to create child NPM locks.
    if (!lockFile) {
      for (let ancestor = directory; ; ancestor = path.dirname(ancestor)) {
        lockDirectories.add(ancestor);
        if (ancestor === root) break;
      }
    }
    const alternateLocks = [...lockDirectories].flatMap(dir => ['pnpm-lock.yaml', 'yarn.lock', 'bun.lock', 'bun.lockb']
      .map(name => path.join(dir, name)).filter(fs.existsSync));
    const alternateManager = [json.packageManager, owner.json.packageManager]
      .some(value => typeof value === 'string' && /^(pnpm|yarn|bun)@/.test(value));
    const unsupportedManager = alternateLocks.length > 0 || alternateManager;
    if (unsupportedManager)
      report.findings.push({ rule: 'FE003', classification: 'unknown', path: relative(root, file),
        evidence: 'A non-NPM package manager or lockfile was detected; its dependency policy is unsupported by this audit.',
        recommendation: 'Preserve the current package manager and inspect lock ownership manually; do not migrate to NPM.',
        lockFiles: alternateLocks.map(candidate => relative(root, candidate)) });
    else if (!lockFile && pkg.workspacePatterns !== null && !ownershipUnknown)
      add('FE003', file, 'No NPM lockfile found for the discovered package/workspace owner.', 'Confirm package manager and lock owner before generating a lock.');
    if (lockFile) {
      try {
        const lock = readJson(lockFile, root);
        if (!lock || typeof lock !== 'object' || Array.isArray(lock) || ![1, 2, 3].includes(lock.lockfileVersion))
          throw new Error('Unsupported lock');
      } catch {
        report.findings.push({ rule: 'FE003', classification: 'unknown', path: relative(root, lockFile), evidence: 'Lockfile is unreadable, invalid or has an unsupported version.' });
      }
    }
    const effectiveEngines = { ...owner.json.engines, ...json.engines };
    if (!unsupportedManager && !ownershipUnknown && (!effectiveEngines.node || !effectiveEngines.npm))
      add('FE001', file, 'Node/NPM engine declarations are incomplete at the package and its workspace owner.', 'Check shared runtime policy and CI pins before adding requirements.');
    const scripts = json.scripts && typeof json.scripts === 'object' ? json.scripts : {};
    for (const [name, command] of Object.entries(scripts)) {
      if (typeof command === 'string' && /(^|[^&])&([^&]|$)/.test(command))
        add('FE002', file, `Script ${name} contains a single ampersand; quoting and shell semantics need review.`, 'Use explicit task sequencing for dependent steps; verify failure propagation.');
    }
    const dependencies = { ...json.dependencies, ...json.devDependencies };
    if (dependencies.typescript && !Object.prototype.hasOwnProperty.call(scripts, 'typecheck'))
      add('FE008', file, 'No explicit typecheck command is declared.', 'Inspect ts-loader/checker gating before deciding whether a separate command is needed.');
    report.projects.push({ packagePath: relative(root, directory), lockOwner: ownershipUnknown ? null : relative(root, owner.directory),
      lockFile: !ownershipUnknown && lockFile ? relative(root, lockFile) : null,
      engines: Object.fromEntries(Object.entries(json.engines || {}).filter(([key]) => ['node', 'npm'].includes(key)).map(([key, value]) => [key, safeSpec(value)])),
      effectiveEngines: ownershipUnknown ? null : Object.fromEntries(Object.entries(effectiveEngines).filter(([key]) => ['node', 'npm'].includes(key)).map(([key, value]) => [key, safeSpec(value)])),
      scripts: Object.keys(scripts).sort(),
      tools: Object.fromEntries(Object.entries(dependencies).filter(([name]) => /^(webpack($|-)|typescript$|ts-loader$|sass($|-)|node-sass$|gulp($|-)|bootstrap$|postcss($|-)|autoprefixer$|cssnano$|@fortawesome\/fontawesome|@fontsource\/)/.test(name)).sort(([a], [b]) => a.localeCompare(b)).map(([key, value]) => [key, safeSpec(value)])),
      configurationFiles: files.filter(candidate => path.dirname(candidate) === directory && /^(webpack.*\.(js|cjs|mjs)|gulpfile\.(js|cjs|mjs)|tsconfig.*\.json)$/.test(path.basename(candidate))).map(candidate => relative(root, candidate)) });
  }
  report.exceptions = (config?.exceptions || []).map(item => ({ ...item,
    matched: report.findings.some(finding => finding.rule === item.rule && (item.path === '*' || finding.path === item.path)) }));
  report.scopedStyles = files.filter(file => /\.(razor|cshtml)\.scss$/.test(file)).map(file => relative(root, file));
  report.checks.push({ name: 'static-inventory', result: report.findings.some(x => x.classification === 'unknown') ? 'incomplete' : 'completed' });
  return report;
}

if (require.main === module) {
  const args = process.argv.slice(2);
  if (args.length !== 2 || args[0] !== '--root') {
    process.stderr.write('Usage: node audit-toolchain.cjs --root <repository>\n');
    process.exitCode = 2;
  } else {
    try {
      const report = audit(args[1]);
      process.stdout.write(JSON.stringify(report, null, 2) + '\n');
      process.exitCode = report.findings.some(x => x.classification === 'unknown') ? 2 : 0;
    } catch (error) {
      // Do not expose raw parser input, registry configuration or environment.
      process.stderr.write(`Inventory failed: ${error.code || (error.message.startsWith('umbrella-frontend.json:') ? error.message : 'invalid or inaccessible input')}\n`);
      process.exitCode = 2;
    }
  }
}
module.exports = { audit, workspaceMatches };
