const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const { spawnSync } = require("node:child_process");

function runNpm(args)
{
	if (!process.env.npm_execpath)
	{
		throw new Error("Run this script through npm run publish:feed so it uses the authenticated npm environment.");
	}

	const result = spawnSync(process.execPath, [process.env.npm_execpath, ...args], { encoding: "utf8" });
	if (result.error)
	{
		throw result.error;
	}

	return result;
}

function publish(directory, execute = runNpm)
{
	const info = JSON.parse(fs.readFileSync(path.join(directory, "pack-info.json"), "utf8"));
	if (info.name !== "umbrella-eslint-config" || path.basename(info.filename) !== info.filename || !info.filename.endsWith(".tgz"))
	{
		throw new Error("Unexpected package identity or tarball path.");
	}

	const tarball = path.join(directory, info.filename);
	const integrity = "sha512-" + crypto.createHash("sha512").update(fs.readFileSync(tarball)).digest("base64");
	if (integrity !== info.integrity)
	{
		throw new Error("Tarball integrity differs from the tested package.");
	}

	const identity = `${info.name}@${info.version}`;
	const existing = execute(["view", identity, "dist.integrity", "--json"]);
	if (existing.status === 0)
	{
		if (JSON.parse(existing.stdout || "null") !== integrity)
		{
			throw new Error(`${identity} already exists with different contents. Bump Version in Directory.Build.props.`);
		}

		return `Skipped identical ${identity}.`;
	}

	// Only npm's explicit not-found response permits publication; auth/network failures must stop the job.
	let code;
	for (const output of [existing.stdout, existing.stderr])
	{
		try { code = JSON.parse(output).error?.code ?? code; } catch { /* Not JSON. */ }
	}

	if (code !== "E404")
	{
		throw new Error(`Cannot check ${identity} (${code ?? "npm failure"}); publication aborted.`);
	}

	const tag = info.version.includes("-") ? "next" : "latest";
	const result = execute(["publish", tarball, "--tag", tag, "--ignore-scripts"]);
	if (result.status !== 0)
	{
		throw new Error(`Publishing ${identity} failed (exit ${result.status}). Check feed permissions and npm's pipeline log.`);
	}

	return `Published ${identity} with tag ${tag}.`;
}

module.exports = { publish };

if (require.main === module)
{
	try { console.log(publish(__dirname)); }
	catch (error) { console.error(error.message); process.exitCode = 1; }
}
