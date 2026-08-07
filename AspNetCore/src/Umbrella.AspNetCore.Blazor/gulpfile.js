const gulp = require("gulp");
const gulpSass = require("gulp-sass");
const dartSass = require("sass");
const { spawn } = require("child_process");
const rename = require("gulp-rename");
const postcss = require("gulp-postcss");
const cssnano = require("cssnano");

const sass = gulpSass(dartSass);

function createWebpackTask(options = {})
{
	const { mode, analyze = false, watch = false } = options;

	return function webpackTask()
	{
		const args = [require.resolve("webpack-cli/bin/cli.js")];

		if (mode)
		{
			args.push("--mode", mode);
		}

		if (analyze)
		{
			args.push("--analyze");
		}

		if (watch)
		{
			args.push("--watch");
		}

		return new Promise((resolve, reject) =>
		{
			const webpackProcess = spawn(process.execPath, args, { stdio: "inherit" });

			webpackProcess.on("error", reject);
			webpackProcess.on("close", code => code === 0
				? resolve()
				: reject(new Error(`webpack exited with code ${code}`)));
		});
	};
}

async function lintTask()
{
	const { ESLint } = require("eslint");
	const eslint = new ESLint({ cwd: __dirname });
	const results = await eslint.lintFiles(["."]);
	const formatter = await eslint.loadFormatter("stylish");
	const output = await formatter.format(results);

	if (output)
	{
		console.log(output);
	}

	// Warnings fail the build too, otherwise a rule configured as "warn" is reported but never enforced.
	const errorCount = results.reduce((total, result) => total + result.errorCount, 0);
	const warningCount = results.reduce((total, result) => total + result.warningCount, 0);

	if (errorCount > 0 || warningCount > 0)
	{
		throw new Error(`eslint reported ${errorCount} error(s) and ${warningCount} warning(s).`);
	}
}

gulp.task("lint", lintTask);

const buildWebpack = createWebpackTask();
const buildAnalyzeWebpack = createWebpackTask({ analyze: true });
const buildReleaseWebpack = createWebpackTask({ mode: "production" });

const paths = {
	sourceNoUnderscores: [
		'./**/*.razor.scss',
		'!./**/_*.razor.scss'
	],
	sourceUnderscores: [
		'./**/_*.razor.scss'
	],
	sourceAll: [
		'./**/*.razor.scss'
	],
	output: "./",
	sourceClean: [
		'./**/*.razor.css'
	]
};

const sassOptions = { loadPaths: ['node_modules'] };

function createSassBuildTasks(minify = false)
{
	return gulp.series(
		function noUnderscores()
		{
			let stream = gulp.src(paths.sourceNoUnderscores)
				.pipe(sass(sassOptions).on("error", sass.logError));

			if (minify)
			{
				stream = stream.pipe(postcss([cssnano()]));
			}

			return stream.pipe(gulp.dest(paths.output));
		},
		function underscores()
		{
			let stream = gulp.src(paths.sourceUnderscores)
				.pipe(rename(path => path.basename = path.basename.replace(/^_/, '')))
				.pipe(sass(sassOptions).on("error", sass.logError));

			if (minify)
			{
				stream = stream.pipe(postcss([cssnano()]));
			}

			return stream
				.pipe(rename(path => path.basename = `_${path.basename}`))
				.pipe(gulp.dest(paths.output));
		}
	);
}

gulp.task("build-scoped-sass", createSassBuildTasks(false));

gulp.task("build-release-scoped-sass", createSassBuildTasks(true));

gulp.task("build", gulp.series("lint", buildWebpack, "build-scoped-sass"));

gulp.task("build-analyze", gulp.series("lint", buildAnalyzeWebpack, "build-scoped-sass"));

gulp.task("build-release", gulp.series("lint", buildReleaseWebpack, "build-release-scoped-sass"));

gulp.task("clean-scoped-sass", async () =>
{
	var del = await import("del");

	return await del.deleteAsync(paths.sourceClean);
});
