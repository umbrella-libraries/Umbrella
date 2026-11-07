const assert = require("node:assert/strict");
const { test } = require("node:test");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { spawnSync } = require("node:child_process");
const root = path.resolve(__dirname, "../../..");
const packScript = path.join(root, "Tools/src/Umbrella.ESLint.Config/scripts/Pack.ps1");

function run(command, args, options = {})
{
	const result = spawnSync(command, args, { cwd: root, encoding: "utf8", timeout: 240000, ...options });
	assert.equal(result.status, 0, `${command}: ${result.error || ""}\n${result.stdout}\n${result.stderr}`);
	return result.stdout;
}

test("stamps only staged files, produces a deterministic portable package and rejects invalid versions", { timeout: 300000 }, t =>
{
	const temporary = fs.mkdtempSync(path.join(os.tmpdir(), "umbrella-package-test-"));
	t.after(() => fs.rmSync(temporary, { recursive: true, force: true }));
	const tracked = ["package.json", "package-lock.json", "Tools/src/Umbrella.ESLint.Config/package.json", "AspNetCore/src/Umbrella.AspNetCore.Blazor/package.json"];
	const before = tracked.map(file => fs.readFileSync(path.join(root, file)));
	assert.notEqual(spawnSync("pwsh", ["-NoProfile", "-File", packScript, "-OutputDirectory", root], { encoding: "utf8" }).status, 0);
	const output = path.join(root, "artifacts/npm");
	const info = JSON.parse(fs.readFileSync(path.join(output, "pack-info.json"), "utf8"));
	const tarball = path.join(output, info.filename);
	const manifest = JSON.parse(run("tar", ["-xOf", tarball, "package/package.json"]));
	assert.equal(manifest.version, fs.readFileSync(path.join(root, "Directory.Build.props"), "utf8").match(/<Version>([^<]+)<\/Version>/)[1]);
	assert.equal(manifest.private, undefined);
	assert.equal(manifest.devDependencies, undefined);
	assert.equal(manifest.scripts, undefined);
	assert.deepEqual(run("tar", ["-tf", tarball]).trim().split(/\r?\n/).sort(), ["package/LICENSE", "package/README.md", "package/index.cjs", "package/package.json"].sort());

	run("pwsh", ["-NoProfile", "-File", packScript, "-OutputDirectory", path.join(temporary, "repeat")]);
	const repeated = JSON.parse(fs.readFileSync(path.join(temporary, "repeat/pack-info.json"), "utf8"));
	assert.equal(repeated.integrity, info.integrity, "Repeated packing must preserve bytes for safe retries");

	const versionFile = path.join(temporary, "version.props");
	fs.writeFileSync(versionFile, "<Project><PropertyGroup><Version>10.0.0</Version></PropertyGroup></Project>");
	run("pwsh", ["-NoProfile", "-File", packScript, "-VersionFile", versionFile, "-OutputDirectory", path.join(temporary, "stable")]);
	assert.equal(JSON.parse(fs.readFileSync(path.join(temporary, "stable/pack-info.json"), "utf8")).tag, "latest");
	fs.writeFileSync(versionFile, "<Project><PropertyGroup><Version>10.0.0-preview.0115</Version></PropertyGroup></Project>");
	assert.notEqual(spawnSync("pwsh", ["-NoProfile", "-File", packScript, "-VersionFile", versionFile, "-OutputDirectory", path.join(temporary, "invalid")], { encoding: "utf8" }).status, 0);
	assert.equal(fs.existsSync(path.join(temporary, "invalid")), false);

	const consumer = path.join(temporary, "consumer");
	fs.mkdirSync(consumer);
	fs.writeFileSync(path.join(consumer, "package.json"), JSON.stringify({ name: "umbrella-package-test-consumer", private: true, dependencies: {
		"@umbrella-libraries/eslint-config": `file:${tarball.replaceAll("\\", "/")}`,
		eslint: "10.12.0", typescript: "npm:@typescript/typescript6@6.0.2"
	} }));
	run(process.execPath, [process.env.npm_execpath, "install", "--ignore-scripts", "--no-audit", "--no-fund"], { cwd: consumer });
	run(process.execPath, ["--test", path.join(__dirname, "config.test.cjs")], { cwd: consumer, env: { ...process.env, UMBRELLA_ESLINT_CONSUMER: consumer } });
	for (const [index, file] of tracked.entries())
	{
		assert.deepEqual(fs.readFileSync(path.join(root, file)), before[index], `${file} changed during packaging`);
	}
});
