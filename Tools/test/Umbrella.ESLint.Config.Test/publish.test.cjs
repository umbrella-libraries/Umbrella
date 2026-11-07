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

for (const stdout of ["", " \r\n", "null", '""'])
{
	test(`downloads and hashes identical bytes when integrity metadata is ${JSON.stringify(stdout)}`, t =>
	{
		const { directory, info } = fixture(t);
		const localBytes = fs.readFileSync(path.join(directory, info.filename));
		const calls = [];
		let downloadDirectory;
		const result = publish(directory, args =>
		{
			calls.push(args);
			if (args[0] === "view") return { status: 0, stdout };
			assert.equal(args[0], "pack", "Must never publish an existing version");
			downloadDirectory = args.at(-1);
			assert.notEqual(downloadDirectory, directory);
			fs.writeFileSync(path.join(downloadDirectory, info.filename), localBytes);
			// The comparison must hash the downloaded bytes, not trust npm's JSON hash.
			return { status: 0, stdout: JSON.stringify([{ filename: info.filename, integrity: "sha512-untrusted" }]) };
		});
		assert.match(result, /Skipped identical/);
		assert.deepEqual(calls[1], ["pack", `${info.name}@${info.version}`, "--json", "--ignore-scripts", "--prefer-online", "--pack-destination", downloadDirectory]);
		assert.equal(fs.existsSync(downloadDirectory), false);
		assert.deepEqual(fs.readFileSync(path.join(directory, info.filename)), localBytes);
	});
}

test("missing integrity with different downloaded bytes still requires a version bump", t =>
{
	const { directory, info } = fixture(t);
	let downloadDirectory;
	assert.throws(() => publish(directory, args =>
	{
		if (args[0] === "view") return { status: 0, stdout: "" };
		assert.equal(args[0], "pack");
		downloadDirectory = args.at(-1);
		fs.writeFileSync(path.join(downloadDirectory, info.filename), "different published bytes");
		return { status: 0, stdout: JSON.stringify([{ filename: info.filename, integrity: info.integrity }]) };
	}), /Bump Version/);
	assert.equal(fs.existsSync(downloadDirectory), false);
});

test("download failures remain fatal and clean up temporary files", t =>
{
	const { directory } = fixture(t);
	for (const code of ["E401", "E403", "E404", "ECONNRESET", "ETIMEDOUT"])
	{
		let downloadDirectory;
		assert.throws(() => publish(directory, args =>
		{
			if (args[0] === "view") return { status: 0, stdout: "" };
			assert.equal(args[0], "pack");
			downloadDirectory = args.at(-1);
			fs.writeFileSync(path.join(downloadDirectory, "partial.tgz"), "incomplete download");
			return { status: 1, stderr: JSON.stringify({ error: { code } }) };
		}), /Cannot download existing .*publication aborted/);
		assert.equal(fs.existsSync(downloadDirectory), false);
	}
});

test("malformed download results and missing tarballs never permit publication", t =>
{
	const { directory } = fixture(t);
	for (const stdout of ["not json", "[]", '[{"filename":"../outside.tgz"}]', '[{"filename":"missing.tgz"}]'])
	{
		let downloadDirectory;
		assert.throws(() => publish(directory, args =>
		{
			if (args[0] === "view") return { status: 0, stdout: "" };
			assert.equal(args[0], "pack");
			downloadDirectory = args.at(-1);
			return { status: 0, stdout };
		}));
		assert.equal(fs.existsSync(downloadDirectory), false);
	}
});
