# VS Code Extension — Build and Publishing Guide

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- [Node.js 18+](https://nodejs.org/) and npm
- `@vscode/vsce` — installed as a dev dependency, no global install required

---

## Building a VSIX

Each platform produces a separate VSIX containing a self-contained .NET backend binary.
Run the appropriate command from `ai-dev-vscode/`:

```bash
npm run package:win      # Windows x64  → ai-dev-studio-win32-x64-0.1.x.vsix
npm run package:mac      # macOS ARM     → ai-dev-studio-darwin-arm64-0.1.x.vsix
npm run package:linux    # Linux x64     → ai-dev-studio-linux-x64-0.1.x.vsix
npm run package:all      # All three platforms in one go
```

Each command:
1. Compiles the TypeScript/React extension with esbuild
2. Runs `dotnet publish --self-contained` for the target platform
3. Copies the renamed binary into `bin/`
4. Packages the VSIX with `vsce package --target <platform>`
5. Removes `bin/` before the next platform

The `.vsix` files are written to `ai-dev-vscode/` and are listed in `.gitignore`.

---

## Bumping the version

Edit the `version` field in `ai-dev-vscode/package.json` before packaging.
Use [semver](https://semver.org/): `MAJOR.MINOR.PATCH`.

---

## Sideloading (no Marketplace account required)

Share the `.vsix` file with a developer, then they install it one of two ways:

**VS Code UI:** Extensions panel → `···` menu → **Install from VSIX…**

**Terminal:**
```bash
code --install-extension ai-dev-studio-win32-x64-0.1.1.vsix
```

The extension auto-starts the bundled backend when it detects a `.ai-dev/project.json`
file in the open workspace. No separate installation is required.

---

## Publishing to the VS Code Marketplace

### One-time setup

1. **Create a Microsoft account** (or use an existing one) at [microsoft.com](https://microsoft.com).

2. **Create an Azure DevOps organisation** at [dev.azure.com](https://dev.azure.com).

3. **Generate a Personal Access Token (PAT):**
   - In Azure DevOps: User settings → Personal access tokens → New Token
   - Set organisation: **All accessible organisations**
   - Set scope: **Marketplace → Manage**
   - Copy the token — it is only shown once

4. **Create a publisher** at [marketplace.visualstudio.com/manage](https://marketplace.visualstudio.com/manage):
   - Choose a publisher ID (permanent, part of the extension's unique ID)
   - Update `"publisher"` in `ai-dev-vscode/package.json` to match

5. **Authenticate vsce:**
   ```bash
   cd ai-dev-vscode
   npx vsce login <your-publisher-id>
   # paste the PAT when prompted
   ```

### Publishing

Build all three platform VSIXs, then publish each:

```bash
npm run package:all

npx vsce publish --packagePath ai-dev-studio-win32-x64-0.1.1.vsix
npx vsce publish --packagePath ai-dev-studio-darwin-arm64-0.1.1.vsix
npx vsce publish --packagePath ai-dev-studio-linux-x64-0.1.1.vsix
```

Or build and publish in one step (requires `vsce login` first):

```bash
npx vsce publish --target win32-x64
npx vsce publish --target darwin-arm64
npx vsce publish --target linux-x64
```

The extension will be live on the Marketplace within a few minutes.

### Re-publishing an update

1. Bump the version in `package.json`
2. Run `npm run package:all`
3. Publish each VSIX as above

> Publishing the same version number twice will fail — always bump the version first.
