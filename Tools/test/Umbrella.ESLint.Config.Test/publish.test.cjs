const assert = require("node:assert/strict");
const { test } = require("node:test");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const crypto = require("node:crypto");
const { publish } = require("../../src/Umbrella.ESLint.Config/scripts/Publish.cjs");

function fixture(t, version = "10.0.0-preview-0115")
{
	const directory = fs.mkdtempSync(path.join(os.tmpdir(), "umbrella-publish-test-"));
	t.after(() => fs.rmSync(directory, { recursive: true, force: true }));
	const bytes = Buffer.from("tested tarball");
	const info = { name: "umbrella-eslint-config", version, filename: "package.tgz", integrity: "sha512-" + crypto.createHash("sha512").update(bytes).digest("base64") };
	fs.writeFileSync(path.join(directory, info.filename), bytes);
	fs.writeFileSync(path.join(directory, "pack-info.json"), JSON.stringify(info));
	return { directory, info };
}

test("skips only identical already-published bytes", t =>
{
	const { directory, info } = fixture(t);
	const calls = [];
	assert.match(publish(directory, args => { calls.push(args); return { status: 0, stdout: JSON.stringify(info.integrity) }; }), /Skipped identical/);
	assert.equal(calls.length, 1);
	assert.throws(() => publish(directory, () => ({ status: 0, stdout: '"sha512-other"' })), /Bump Version/);
});

for (const [version, tag] of [["10.0.0-preview-0115", "next"], ["10.0.0", "latest"]])
{
	test(`publishes the tested tarball with ${tag}`, t =>
	{
		const { directory } = fixture(t, version);
		const calls = [];
		assert.match(publish(directory, args =>
		{
			calls.push(args);
			return args[0] === "view" ? { status: 1, stdout: JSON.stringify({ error: { code: "E404" } }) } : { status: 0 };
		}), /Published/);
		assert.deepEqual(calls[1], ["publish", path.join(directory, "package.tgz"), "--tag", tag, "--ignore-scripts"]);
	});
}

test("auth, network, malformed responses and publish failures fail closed", t =>
{
	const { directory } = fixture(t);
	for (const code of ["E401", "E403", "ECONNRESET", "ETIMEDOUT"])
	{
		assert.throws(() => publish(directory, () => ({ status: 1, stderr: JSON.stringify({ error: { code } }) })), /publication aborted/);
	}
	assert.throws(() => publish(directory, () => ({ status: 1, stdout: "not json" })), /publication aborted/);
	assert.throws(() => publish(directory, args => args[0] === "view" ? { status: 1, stdout: '{"error":{"code":"E404"}}' } : { status: 1 }), /Publishing .* failed/);
});

test("rejects a changed tarball before contacting the registry", t =>
{
	const { directory } = fixture(t);
	fs.appendFileSync(path.join(directory, "package.tgz"), "changed");
	assert.throws(() => publish(directory, () => assert.fail("Registry must not be contacted")), /integrity/);
});
