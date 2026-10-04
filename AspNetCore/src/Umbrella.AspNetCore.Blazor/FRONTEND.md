# Frontend toolchain

Run frontend commands from this directory. The packages require Node.js 22.22.3+ on the 22.x line, 24.15.0+ on the 24.x line, or 26.0.0+, and npm 10+.

```sh
npm ci
npm run typecheck
npm test
npm run build
npm run build-release
```

TypeScript 7.0.2 is installed as `@typescript/native` through an npm alias. It provides `tsc` and checks the complete project, including frontend tests. Gulp runs this check before linting and bundling in development, release, and analysis builds; a compiler error stops the build.

Type-aware ESLint and `ts-loader` still require the TypeScript 6 compiler API. The `typescript` dependency therefore aliases Microsoft's `@typescript/typescript6` compatibility package. It provides that API and a separate `tsc6` executable. Webpack uses `ts-loader` only to transpile after the TypeScript 7 check succeeds. MSBuild compilation remains blocked so npm owns the JavaScript assets.

This follows [Microsoft's guidance for running TypeScript 7 alongside API-based tools](https://devblogs.microsoft.com/typescript/announcing-typescript-7-0/#running-side-by-side-with-typescript-60). Keep both aliases until the loader and lint packages support the TypeScript 7 API.
