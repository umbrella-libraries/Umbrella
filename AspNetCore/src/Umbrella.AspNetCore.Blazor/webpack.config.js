const path = require("path");
const paths = require("./webpack.paths");
const webpack = require("webpack");
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
const BundleAnalyzerPlugin = require("webpack-bundle-analyzer").BundleAnalyzerPlugin;
const TerserJsPlugin = require("terser-webpack-plugin");
const CssMinimizerWebpackPlugin = require("css-minimizer-webpack-plugin");
const autoprefixer = require('autoprefixer');

module.exports = async (env, argv) =>
{
	// Default to development mode
	let isDevMode = true;

	if (argv.mode)
		isDevMode = argv.mode === "development";

	const analyze = argv.analyze || false;

	console.log(`Development Mode: ${isDevMode}`);
	console.log(`Bundle Analyzer: ${analyze}`);

	const devtool = isDevMode ? "cheap-module-source-map" : "hidden-source-map";

	return [{
		mode: isDevMode ? "development" : "production",
		cache: isDevMode ? { type: "filesystem" } : false,
		devtool,
		performance: isDevMode ? false : {
			hints: "warning",
			maxEntrypointSize: 360000
		},
		stats: "errors-warnings",
		entry: {
			"umbrella-blazor": "scripts"
		},
		resolve: {
			extensions: ['.js', '.ts', '.json'],
			alias: {
				styles: paths.styles,
				scripts: paths.scripts
			}
		},
		output: {
			clean: true,
			chunkFilename: "[name].js",
			path: path.resolve(__dirname, paths.dist),
			publicPath: paths.publicPath
		},
		module: {
			rules: [
				{ test: /\.ts$/, exclude: /(node_modules|bower_components)/, use: "ts-loader" },
				{
					test: /\.(css|scss)$/,
					exclude: /(node_modules|bower_components)/,
					use: [MiniCssExtractPlugin.loader,
					{
						loader: 'css-loader',
						options: { sourceMap: true }
					},
					{
						loader: 'postcss-loader',
						options: {
							postcssOptions: {
								plugins: [
									autoprefixer()
								]
							},
							sourceMap: true
						}
					},
					{
						loader: "resolve-url-loader",
						options: { sourceMap: true }
					},
					{
						loader: 'sass-loader',
						options: { sourceMap: true }
					}
					]
				}
			]
		},
		optimization: {
			minimize: !isDevMode,
			minimizer: [
				new TerserJsPlugin({
					parallel: true,
					terserOptions: {
						ecma: 2016,
						compress: {
							passes: 2
						},
						format: {
							comments: false
						},
						keep_fnames: true
					},
					extractComments: false
				}),
				new CssMinimizerWebpackPlugin({
					minimizerOptions: {
						discardComments: { removeAll: true },
					}
				})
			].filter(x => x)
		},
		plugins: [
			new MiniCssExtractPlugin({
				filename: "[name].css"
			}),
		].concat(analyze ? [new BundleAnalyzerPlugin({ analyzerMode: "static" })] : []).filter(x => x)
	}];
};
