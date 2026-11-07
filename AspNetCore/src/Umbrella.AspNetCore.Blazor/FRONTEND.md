# Frontend toolchain

Install dependencies from the repository root with `npm ci`; the root owns the workspace lockfile. Run frontend commands from this directory after installation. The packages require Node.js 22.22.3+ on the 22.x line, 24.15.0+ on the 24.x line, or 26.0.0+, and npm 10+.

```sh
npm run typecheck
npm test
npm run build
npm run build-release
```

TypeScript 7.0.2 is installed as `@typescript/native` through an npm alias. It provides `tsc` and checks the complete project, including frontend tests. Gulp runs this check before linting and bundling in development, release, and analysis builds; a compiler error stops the build.

Type-aware ESLint and `ts-loader` still require the TypeScript 6 compiler API. The `typescript` dependency therefore aliases Microsoft's `@typescript/typescript6` compatibility package. It provides that API and a separate `tsc6` executable. Webpack uses `ts-loader` only to transpile after the TypeScript 7 check succeeds. MSBuild compilation remains blocked so npm owns the JavaScript assets.

This follows [Microsoft's guidance for running TypeScript 7 alongside API-based tools](https://devblogs.microsoft.com/typescript/announcing-typescript-7-0/#running-side-by-side-with-typescript-60). Keep both aliases until the loader and lint packages support the TypeScript 7 API.

The shared lint rules live in `Tools/src/Umbrella.ESLint.Config` and are linked by NPM workspaces. Edits take effect on the next lint/build run without publishing. Local overrides belong in this project's `eslint.config.js`. See the shared package README for external consumption and release instructions.

Root commands also support `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`, and `npm run build-release`. CI uses Node 24.16.0 and NPM 11.13.0. Run the asset build before `dotnet build` or `dotnet pack`; MSBuild does not restore or compile NPM assets automatically.
