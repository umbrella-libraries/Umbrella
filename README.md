# Umbrella
![alt text](https://dev.azure.com/zinofi/Umbrella%20Code%20Libraries/_apis/build/status/Build "Build Status")
![alt text](https://vsrm.dev.azure.com/zinofi/_apis/public/Release/badge/5c7fe595-1c0e-45c7-a704-e2f5bff37b1a/2/2 "Release Status")

## Frontend workspace

Run `npm ci` from the repository root using NPM 11.13.0 (CI: Node 24.16.0).
`npm run lint`, `npm run typecheck`, `npm test`, and `npm run build-release`
validate and build the Blazor frontend. Build frontend assets before the .NET project.

The [shared ESLint package](Tools/src/Umbrella.ESLint.Config/README.md) is consumed
locally through NPM workspaces and published to the Umbrella development feed by CI.
See its README for project overrides, package tests and release instructions.
