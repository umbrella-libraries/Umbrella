const assert = require("node:assert/strict");
const { test, before, after } = require("node:test");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { createRequire } = require("node:module");
const { pathToFileURL } = require("node:url");

const packageRoot = process.env.UMBRELLA_ESLINT_CONSUMER || path.resolve(__dirname, "../../src/Umbrella.ESLint.Config");
const fromPackage = createRequire(path.join(packageRoot, "package.json"));
const configPath = process.env.UMBRELLA_ESLINT_CONSUMER ? fromPackage.resolve("umbrella-eslint-config") : path.join(packageRoot, "index.cjs");
const umbrella = fromPackage(configPath);
const { ESLint } = fromPackage("eslint");
let fixture;

before(() =>
{
	fixture = fs.mkdtempSync(path.join(os.tmpdir(), "umbrella-eslint-test-"));
	fs.writeFileSync(path.join(fixture, "tsconfig.json"), JSON.stringify({ compilerOptions: { strict: true, target: "ES2024" }, include: ["**/*.ts"] }));
	fs.writeFileSync(path.join(fixture, "example.ts"), "export const value = 1;\r\n");
});

after(() => fs.rmSync(fixture, { recursive: true, force: true }));

function linter(overrides = [])
{
	return new ESLint({
		cwd: fixture,
		overrideConfigFile: true,
		overrideConfig: [...umbrella({ tsconfigRootDir: fixture, files: ["**/*.ts"], ignores: ["generated/**"] }), ...overrides]
	});
}

test("works as a CommonJS export and an ESM default import", async () =>
{
	assert.equal((await import(pathToFileURL(configPath).href)).default, umbrella);
});

test("uses the consumer tsconfig and detects a type-aware violation", async () =>
{
	const [clean] = await linter().lintText("export const value = 1;\r\n", { filePath: "example.ts" });
	assert.equal(clean.errorCount, 0, JSON.stringify(clean.messages));
	const [result] = await linter().lintText("export const value = 1;\r\nPromise.resolve(value);\r\n", { filePath: "example.ts" });
	assert.ok(result.messages.some(message => message.ruleId === "@typescript-eslint/no-floating-promises"));
	assert.ok(!result.messages.some(message => message.fatal), JSON.stringify(result.messages));
});

test("enforces formatting and allows project overrides", async () =>
{
	const source = "export const value = 'hello'\n";
	const [result] = await linter().lintText(source, { filePath: "example.ts" });
	for (const rule of ["@stylistic/quotes", "@stylistic/semi", "@stylistic/linebreak-style"])
	{
		assert.ok(result.messages.some(message => message.ruleId === rule), rule);
	}
	const [overridden] = await linter([{ rules: { "@stylistic/quotes": ["error", "single"] } }])
		.lintText("export const value = 'hello';\r\n", { filePath: "example.ts" });
	assert.equal(overridden.errorCount, 0, JSON.stringify(overridden.messages));
});

test("keeps shared and additional exclusions global", async () =>
{
	const eslint = linter();
	for (const file of ["bin/a.ts", "nested/obj/a.ts", "wwwroot/a.ts", "node_modules/a.ts", "types.d.ts", "generated/a.ts"])
	{
		assert.equal(await eslint.isPathIgnored(path.join(fixture, file)), true, file);
	}
	assert.equal(await eslint.isPathIgnored(path.join(fixture, "example.ts")), false);
});

test("enforces all existing house-style rules and can fix the violations", async () =>
{
	const source = "\ufeffexport function greet() {\n  return 'hello'  \n}";
	const [result] = await linter().lintText(source, { filePath: "example.ts" });
	for (const rule of ["brace-style", "indent", "quotes", "semi", "eol-last", "no-trailing-spaces", "linebreak-style"])
	{
		assert.ok(result.messages.some(message => message.ruleId === `@stylistic/${rule}`), rule);
	}
	assert.ok(result.messages.some(message => message.ruleId === "unicode-bom"));
	const fixer = new ESLint({ cwd: fixture, overrideConfigFile: true, fix: true, overrideConfig: umbrella({ tsconfigRootDir: fixture, files: ["**/*.ts"] }) });
	const [fixed] = await fixer.lintText(source, { filePath: "example.ts" });
	assert.equal(fixed.errorCount, 0, JSON.stringify(fixed.messages));
	assert.equal(fixed.output, "export function greet()\r\n{\r\n\treturn \"hello\";\r\n}\r\n");
});

test("requires explicit consumer settings", () =>
{
	assert.throws(() => umbrella(), /tsconfigRootDir/);
	assert.throws(() => umbrella({ tsconfigRootDir: fixture, files: [] }), /files/);
	assert.throws(() => umbrella({ tsconfigRootDir: fixture, files: ["**/*.ts"], ignores: "bin" }), /ignores/);
});
