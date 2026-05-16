import * as esbuild from 'esbuild';

const watch = process.argv.includes('--watch');

const sharedOptions = {
  bundle: true,
  sourcemap: true,
  minify: !watch,
};

// Extension host — Node, externalise vscode
const extensionCtx = await esbuild.context({
  ...sharedOptions,
  entryPoints: ['src/extension.ts'],
  outfile: 'dist/extension.js',
  platform: 'node',
  target: 'node18',
  format: 'cjs',
  external: ['vscode'],
});

// Webview bundles — browser, no vscode module
const webviewCtx = await esbuild.context({
  ...sharedOptions,
  entryPoints: [
    'src/webviews/agents/main.tsx',
    'src/webviews/messages/main.tsx',
    'src/webviews/decisions/main.tsx',
    'src/webviews/logs/main.tsx',
  ],
  outdir: 'dist/webviews',
  platform: 'browser',
  target: 'es2020',
  format: 'iife',
  loader: { '.tsx': 'tsx', '.ts': 'ts' },
});

if (watch) {
  await Promise.all([extensionCtx.watch(), webviewCtx.watch()]);
  console.log('Watching for changes...');
} else {
  await Promise.all([extensionCtx.rebuild(), webviewCtx.rebuild()]);
  await Promise.all([extensionCtx.dispose(), webviewCtx.dispose()]);
  console.log('Build complete.');
}
