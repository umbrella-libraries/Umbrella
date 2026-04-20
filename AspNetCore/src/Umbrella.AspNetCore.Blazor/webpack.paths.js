const path = require('path');

const paths = {
	root: path.resolve(__dirname, './Content'),
	wwwroot: path.resolve(__dirname, './wwwroot'),
	publicPath: "_content/Umbrella.AspNetCore.Blazor/dist/"
};

paths.dist = path.join(paths.wwwroot, "dist");

paths.scripts = path.join(paths.root, "scripts");
paths.styles = path.join(paths.root, "styles");

module.exports = paths;
