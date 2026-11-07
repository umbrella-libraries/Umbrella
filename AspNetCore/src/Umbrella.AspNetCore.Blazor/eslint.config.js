const umbrella = require("umbrella-eslint-config");

module.exports = umbrella({
	tsconfigRootDir: __dirname,
	files: ["Content/scripts/**/*.ts", "FrontendTest/**/*.ts"],
	// These build scripts are outside the type-aware TypeScript program.
	ignores: ["gulpfile.js", "webpack.config.js", "webpack.paths.js"]
});
