#!/usr/bin/env node
/**
 * Publishes the ai-dev.api backend for one or all platforms, copies the
 * renamed binary into bin/, then runs vsce package with the matching target.
 *
 * Usage:
 *   node scripts/bundle-backend.mjs              # all three platforms
 *   node scripts/bundle-backend.mjs win32-x64    # single platform
 */

import { execSync } from 'child_process';
import { cpSync, mkdirSync, rmSync, existsSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const extensionDir = join(__dirname, '..');
const repoRoot = join(extensionDir, '..');
const apiProject = join(repoRoot, 'ai-dev.api', 'ai-dev.api.csproj');
const tfm = 'net10.0';

const TARGETS = [
  {
    vsceTarget: 'win32-x64',
    rid: 'win-x64',
    sourceBinary: 'ai-dev.api.exe',
    destBinary: 'ai-dev-api.exe',
  },
  {
    vsceTarget: 'darwin-arm64',
    rid: 'osx-arm64',
    sourceBinary: 'ai-dev.api',
    destBinary: 'ai-dev-api',
  },
  {
    vsceTarget: 'linux-x64',
    rid: 'linux-x64',
    sourceBinary: 'ai-dev.api',
    destBinary: 'ai-dev-api',
  },
];

const requestedTarget = process.argv[2];
const targets = requestedTarget
  ? TARGETS.filter(t => t.vsceTarget === requestedTarget || t.rid === requestedTarget)
  : TARGETS;

if (targets.length === 0) {
  console.error(`Unknown target "${requestedTarget}". Valid values: ${TARGETS.map(t => t.vsceTarget).join(', ')}`);
  process.exit(1);
}

const binDir = join(extensionDir, 'bin');

for (const { vsceTarget, rid, sourceBinary, destBinary } of targets) {
  console.log(`\n${'─'.repeat(60)}`);
  console.log(`  ${vsceTarget}  (dotnet RID: ${rid})`);
  console.log('─'.repeat(60));

  // 1. Publish self-contained single-file so bin/ only needs the one executable
  run(
    `dotnet publish "${apiProject}" --self-contained -r ${rid} -c Release -p:PublishSingleFile=true`,
    repoRoot,
  );

  // 2. Copy renamed binary into bin/
  const src = join(repoRoot, 'ai-dev.api', 'bin', 'Release', tfm, rid, 'publish', sourceBinary);
  const dest = join(binDir, destBinary);
  mkdirSync(binDir, { recursive: true });
  cpSync(src, dest);
  console.log(`  Copied → bin/${destBinary}`);

  // 3. Package platform-specific VSIX
  run(`npx vsce package --target ${vsceTarget} --no-dependencies`, extensionDir);

  // 4. Clean bin/ before next iteration so it is never stale
  rmSync(binDir, { recursive: true, force: true });
  console.log(`  Cleaned bin/`);
}

console.log('\n✓ Done.\n');

function run(cmd, cwd) {
  console.log(`\n> ${cmd}`);
  execSync(cmd, { stdio: 'inherit', cwd });
}
