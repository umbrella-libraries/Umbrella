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
		files: ["Content/scripts/**/*.ts", "FrontendTest/**/*.ts"],
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
		// Everything below is reported as an error rather than a warning: eslint only exits
		// non-zero for errors, so a warning would leave "npm run lint" and the gulp build green.
		rules: {
			// House style: Allman braces, tabs, double quotes.
			"@stylistic/brace-style": ["error", "allman"],
			"@stylistic/indent": ["error", "tab"],
			"@stylistic/quotes": ["error", "double"],
			"@stylistic/semi": ["error", "always"],

			// Whitespace and encoding rules kept here rather than .editorconfig
			// so that a build can verify them.
			"@stylistic/eol-last": ["error", "always"],
			"@stylistic/no-trailing-spaces": "error",
			"@stylistic/linebreak-style": ["error", "windows"],
			"unicode-bom": ["error", "never"]
		}
	}
);
