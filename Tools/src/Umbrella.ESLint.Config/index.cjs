const path = require("node:path");
const js = require("@eslint/js");
const tseslint = require("typescript-eslint");
const stylistic = require("@stylistic/eslint-plugin");

/**
 * Creates flat configuration objects. Append ordinary ESLint objects to override rules.
 * @param {{ tsconfigRootDir: string, files: string[], ignores?: string[] }} options
 * @returns {import("eslint").Linter.Config[]}
 */
module.exports = function umbrella({ tsconfigRootDir, files, ignores = [] } = {})
{
	if (typeof tsconfigRootDir !== "string" || !path.isAbsolute(tsconfigRootDir))
	{
		throw new TypeError("tsconfigRootDir must be the consuming project's absolute directory.");
	}

	if (!Array.isArray(files) || files.length === 0 || files.some(file => typeof file !== "string" || !file))
	{
		throw new TypeError("files must be a non-empty array of glob patterns.");
	}

	if (!Array.isArray(ignores) || ignores.some(file => typeof file !== "string" || !file))
	{
		throw new TypeError("ignores must be an array of glob patterns.");
	}

	return tseslint.config(
		{
			name: "umbrella/ignores",
			ignores: ["**/bin/**", "**/obj/**", "**/wwwroot/**", "**/node_modules/**", "**/*.d.ts", ...ignores]
		},
		{
			name: "umbrella/typescript",
			files,
			extends: [js.configs.recommended, ...tseslint.configs.recommendedTypeChecked],
			languageOptions: {
				parserOptions: { projectService: true, tsconfigRootDir }
			},
			plugins: { "@stylistic": stylistic },
			rules: {
				"@stylistic/brace-style": ["error", "allman"],
				"@stylistic/indent": ["error", "tab"],
				"@stylistic/quotes": ["error", "double"],
				"@stylistic/semi": ["error", "always"],
				"@stylistic/eol-last": ["error", "always"],
				"@stylistic/no-trailing-spaces": "error",
				"@stylistic/linebreak-style": ["error", "windows"],
				"unicode-bom": ["error", "never"]
			}
		}
	);
};
