'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');
const { audit } = require('./audit-toolchain.cjs');

function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'umbrella-frontend-test-'));
  t.after(() => {
    const resolved = fs.realpathSync(root);
    assert.equal(path.dirname(resolved), fs.realpathSync(os.tmpdir()));
    assert.ok(path.basename(resolved).startsWith('umbrella-frontend-test-'));
    fs.rmSync(resolved, { recursive: true });
  });
  function write(relative, data) {
    const file = path.join(root, relative);
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, typeof data === 'string' ? data : JSON.stringify(data));
  }
  return { root, write };
}

function snapshot(root) {
  const values = [];
  function visit(dir) {
    for (const item of fs.readdirSync(dir, { withFileTypes: true })) {
      const file = path.join(dir, item.name);
      if (item.isDirectory()) visit(file);
      else values.push([path.relative(root, file), crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex')]);
    }
  }
  visit(root);
  return values.sort(([a], [b]) => a.localeCompare(b));
}

test('inventories multiple packages and scoped types without executing configs or writing files', t => {
  const f = fixture(t);
  for (const name of ['Server', 'Client']) {
    f.write(`${name}/package.json`, { engines: { node: '>=20', npm: '>=10' }, devDependencies: { typescript: '6.0.2', webpack: '5.106.2' }, scripts: { typecheck: 'tsc --noEmit', build: 'gulp build' } });
    f.write(`${name}/package-lock.json`, { lockfileVersion: 3 });
  }
  f.write('Server/webpack.config.js', 'throw new Error("must never execute");');
  f.write('Client/Card.razor.scss', '.card { color: red; }');
  f.write('Server/_Layout.cshtml.scss', '.layout { color: blue; }');
  f.write('Server/node_modules/ignored/package.json', '{invalid');
  f.write('Server/obj/ignored/package.json', '{invalid');
  const before = snapshot(f.root);
  const report = audit(f.root);
  assert.equal(report.projects.length, 2);
  assert.equal(report.scopedStyles.length, 2);
  assert.equal(report.findings.length, 0);
  assert.deepEqual(snapshot(f.root), before);
});

test('uses a workspace root lock rather than demanding child locks', t => {
  const f = fixture(t);
  f.write('package.json', { workspaces: ['Web/*'], engines: { node: '>=20', npm: '>=10' } });
  f.write('package-lock.json', { lockfileVersion: 3 });
  f.write('Web/Client/package.json', { engines: { node: '>=20', npm: '>=10' } });
  f.write('Separate/package.json', {});
  const report = audit(f.root);
  assert.equal(report.projects.find(x => x.packagePath === 'Web/Client').lockOwner, '.');
  assert.ok(!report.findings.some(x => x.rule === 'FE003' && x.path === 'Web/Client/package.json'));
  assert.ok(report.findings.some(x => x.rule === 'FE003' && x.path === 'Separate/package.json'));
});

test('reports engine, script, lock and typecheck drift without conflating && with &', t => {
  const f = fixture(t);
  f.write('package.json', { devDependencies: { typescript: '6' }, scripts: { build: 'copy & compile', verify: 'check && build' } });
  const report = audit(f.root);
  assert.deepEqual(report.findings.map(x => x.rule).sort(), ['FE001', 'FE002', 'FE003', 'FE008']);
});

test('exceptions are scoped, visible and distinguish unmatched declarations', t => {
  const f = fixture(t);
  f.write('package.json', {});
  f.write('Child/package.json', {});
  f.write('umbrella-frontend.json', { baselineVersion: 1, exceptions: [
    { rule: 'FE001', path: 'package.json', reason: 'Runtime selected centrally.' },
    { rule: 'FE008', path: '*', reason: 'Checking through loader.' }
  ] });
  const report = audit(f.root);
  assert.equal(report.findings.find(x => x.rule === 'FE001' && x.path === 'package.json').classification, 'intentional-exception');
  assert.equal(report.findings.find(x => x.rule === 'FE001' && x.path === 'Child/package.json').classification, 'drift');
  assert.equal(report.exceptions[1].matched, false);
});

test('malformed input is unknown, not a clean report, and parser text stays private', t => {
  const f = fixture(t);
  f.write('package.json', '{"secret":"sensitive-invalid-json"');
  const report = audit(f.root);
  assert.equal(report.checks[0].result, 'incomplete');
  assert.ok(!JSON.stringify(report).includes('sensitive-invalid-json'));
  const cli = spawnSync(process.execPath, [path.join(__dirname, 'audit-toolchain.cjs'), '--root', f.root], { encoding: 'utf8' });
  assert.equal(cli.status, 2);
});

test('rejects unsupported baselines, invalid rules and escaping exception paths', t => {
  const f = fixture(t);
  for (const config of [ { baselineVersion: 2 },
    { baselineVersion: 1, exceptions: [{ rule: 'FE099', path: '*', reason: 'x' }] },
    { baselineVersion: 1, exceptions: [{ rule: 'FE001', path: '../outside', reason: 'x' }] }
  ]) {
    f.write('umbrella-frontend.json', config);
    assert.throws(() => audit(f.root));
  }
});

test('invalid lock and unsupported workspace patterns remain unknown', t => {
  const f = fixture(t);
  f.write('package.json', { workspaces: ['Web/{Server,Client}'] });
  f.write('package-lock.json', '{invalid');
  assert.equal(audit(f.root).findings.filter(x => x.classification === 'unknown').length, 2);
});

test('does not emit commands or credential-bearing dependency specifications', t => {
  const f = fixture(t);
  f.write('package.json', { scripts: { build: 'TOKEN=secret build' }, dependencies: { webpack: 'https://user:secret@example.org/pkg.tgz' } });
  assert.ok(!JSON.stringify(audit(f.root)).includes('secret'));
});

test('workspace children inherit root engine policy without applying it to standalone packages', t => {
  const f = fixture(t);
  f.write('package.json', { workspaces: ['Web/*'], engines: { node: '>=20', npm: '>=10' } });
  f.write('package-lock.json', { lockfileVersion: 3 });
  f.write('Web/Client/package.json', {});
  f.write('Web/Server/package.json', { engines: { node: '>=22' } });
  f.write('Separate/package.json', {});
  const report = audit(f.root);
  assert.ok(!report.findings.some(x => x.rule === 'FE001' && x.path.startsWith('Web/')));
  assert.ok(report.findings.some(x => x.rule === 'FE001' && x.path === 'Separate/package.json'));
  assert.deepEqual(report.projects.find(x => x.packagePath === 'Web/Client').engines, {});
  assert.deepEqual(report.projects.find(x => x.packagePath === 'Web/Client').effectiveEngines, { node: '>=20', npm: '>=10' });
  assert.deepEqual(report.projects.find(x => x.packagePath === 'Web/Server').effectiveEngines, { node: '>=22', npm: '>=10' });
});

test('workspace children still report engine drift when root and child policies are incomplete', t => {
  const f = fixture(t);
  f.write('package.json', { workspaces: { packages: ['Web/*'] }, engines: { node: '>=20' } });
  f.write('package-lock.json', { lockfileVersion: 3 });
  f.write('Web/Client/package.json', {});
  const report = audit(f.root);
  assert.equal(report.findings.filter(x => x.rule === 'FE001').length, 2);
  assert.equal(report.projects.find(x => x.packagePath === 'Web/Client').lockOwner, '.');
});

test('malformed workspace shapes and unsupported patterns produce incomplete audits', t => {
  const f = fixture(t);
  f.write('package-lock.json', { lockfileVersion: 3 });
  for (const declaration of ['packages/*', null, true, 7, {}, { packages: 'packages/*' },
    [false], [''], ['../outside'], ['packages/{one,two}'], { packages: ['packages/*'], nohoist: [] }]) {
    f.write('package.json', { workspaces: declaration, engines: { node: '>=20', npm: '>=10' } });
    const report = audit(f.root);
    assert.ok(report.findings.some(x => x.rule === 'FE003' && x.classification === 'unknown'), JSON.stringify(declaration));
    assert.equal(report.checks[0].result, 'incomplete');
  }
  const cli = spawnSync(process.execPath, [path.join(__dirname, 'audit-toolchain.cjs'), '--root', f.root], { encoding: 'utf8' });
  assert.equal(cli.status, 2);
});

for (const lockName of ['pnpm-lock.yaml', 'yarn.lock']) {
  test(`${lockName} reports unsupported tooling instead of requesting NPM lock creation`, t => {
    const f = fixture(t);
    f.write('package.json', {});
    f.write(lockName, 'lock content is not executed or interpreted');
    f.write('packages/child/package.json', {});
    const report = audit(f.root);
    assert.ok(!report.findings.some(x => x.rule === 'FE003' && x.classification === 'drift'));
    assert.ok(!report.findings.some(x => x.rule === 'FE001'));
    assert.equal(report.findings.filter(x => x.rule === 'FE003' && x.classification === 'unknown').length, 2);
    assert.equal(report.checks[0].result, 'incomplete');
    assert.deepEqual(report.findings[0].lockFiles, [lockName]);
  });
}

test('mixed locks remain unknown while a nested independent NPM package retains its lock owner', t => {
  const f = fixture(t);
  f.write('package.json', { engines: { node: '>=20', npm: '>=10' } });
  f.write('yarn.lock', 'alternate lock');
  f.write('package-lock.json', { lockfileVersion: 3 });
  f.write('Independent/package.json', { engines: { node: '>=20', npm: '>=10' } });
  f.write('Independent/package-lock.json', { lockfileVersion: 3 });
  const report = audit(f.root);
  assert.equal(report.findings.length, 1);
  assert.equal(report.findings[0].classification, 'unknown');
  assert.equal(report.projects.find(x => x.packagePath === 'Independent').lockOwner, 'Independent');
});

test('an explicit non-NPM package manager is respected even before a lockfile exists', t => {
  const f = fixture(t);
  f.write('package.json', { packageManager: 'pnpm@10.0.0' });
  const report = audit(f.root);
  assert.equal(report.findings.length, 1);
  assert.equal(report.findings[0].classification, 'unknown');
  assert.equal(report.findings[0].rule, 'FE003');
});

test('unsupported ancestor workspace membership leaves descendant locks and engines unknown', t => {
  const f = fixture(t);
  f.write('package.json', { workspaces: ['packages/{app,web}'], engines: { node: '>=20', npm: '>=10' } });
  f.write('package-lock.json', { lockfileVersion: 3 });
  f.write('packages/app/package.json', {});
  const before = snapshot(f.root);
  const report = audit(f.root);
  const findings = report.findings.filter(x => x.path === 'packages/app/package.json');
  assert.deepEqual(findings.map(x => x.rule).sort(), ['FE001', 'FE003']);
  assert.ok(findings.every(x => x.classification === 'unknown'));
  assert.ok(findings.every(x => x.ancestors.includes('package.json')));
  const child = report.projects.find(x => x.packagePath === 'packages/app');
  assert.equal(child.lockOwner, null);
  assert.equal(child.lockFile, null);
  assert.equal(child.effectiveEngines, null);
  assert.equal(report.checks[0].result, 'incomplete');
  assert.deepEqual(snapshot(f.root), before);
});

test('unresolved membership propagates through known nested workspaces but not unrelated siblings', t => {
  const f = fixture(t);
  f.write('package.json', { engines: { node: '>=20', npm: '>=10' } });
  f.write('package-lock.json', { lockfileVersion: 3 });
  f.write('Group/package.json', { workspaces: ['nested/{app,web}'] });
  f.write('Group/nested/package.json', { workspaces: ['*'], engines: { node: '>=22', npm: '>=11' } });
  f.write('Group/nested/package-lock.json', { lockfileVersion: 3 });
  f.write('Group/nested/app/package.json', {});
  f.write('Separate/package.json', {});
  const report = audit(f.root);
  const nested = report.projects.find(x => x.packagePath === 'Group/nested/app');
  assert.equal(nested.lockOwner, null);
  assert.equal(nested.effectiveEngines, null);
  assert.ok(report.findings.filter(x => x.path === 'Group/nested/app/package.json').every(x => x.classification === 'unknown'));
  const siblingFindings = report.findings.filter(x => x.path === 'Separate/package.json');
  assert.deepEqual(siblingFindings.map(x => x.rule).sort(), ['FE001', 'FE003']);
  assert.ok(siblingFindings.every(x => x.classification === 'drift'));
});
