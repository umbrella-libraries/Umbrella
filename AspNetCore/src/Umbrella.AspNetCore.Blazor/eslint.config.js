const js = require("@eslint/js");
const tseslint = require("typescript-eslint");
const stylistic = require("@stylistic/eslint-plugin");

module.exports = tseslint.config(
	{
		ignores: [
			"bin/**",
			"obj/**",
			"wwwroot/**",
			"node_modules/**",
			"**/*.d.ts",

			// Excluded from tsconfig.json, so outside the type-aware program.
			"gulpfile.js",
			"webpack.config.js",
			"webpack.paths.js"
		]
	},
	{
		files: ["Content/scripts/**/*.ts"],
		extends: [
			js.configs.recommended,
			...tseslint.configs.recommendedTypeChecked
		],
		languageOptions: {
			parserOptions: {
				projectService: true,
				tsconfigRootDir: __dirname
			}
		},
		plugins: {
			"@stylistic": stylistic
		},
		rules: {
			// House style: Allman braces, tabs, double quotes.
			"@stylistic/brace-style": ["warn", "allman"],
			"@stylistic/indent": ["warn", "tab"],
			"@stylistic/quotes": ["warn", "double"],
			"@stylistic/semi": ["warn", "always"],

			// Whitespace and encoding rules kept here rather than .editorconfig
			// so that a build can verify them.
			"@stylistic/eol-last": ["warn", "always"],
			"@stylistic/no-trailing-spaces": "warn",
			"@stylistic/linebreak-style": ["warn", "windows"],
			"unicode-bom": ["error", "never"]
		}
	}
);
