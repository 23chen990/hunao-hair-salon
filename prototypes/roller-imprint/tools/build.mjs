import { cpSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join, resolve } from 'node:path';

const ROOT = resolve(fileURLToPath(new URL('..', import.meta.url)));
const DIST = join(ROOT, 'dist');
const SOURCE_FILES = ['index.html', 'styles.css'];
const NETWORK_CODE = /\bfetch\s*\(|XMLHttpRequest|new\s+WebSocket|sendBeacon|https?:\/\//;

for (const relative of SOURCE_FILES) {
  const content = readFileSync(join(ROOT, relative), 'utf8');
  if (NETWORK_CODE.test(content)) throw new Error(`network-capable expression found in ${relative}`);
}

for (const relative of ['model.mjs', 'solver.mjs', 'iaa.mjs', 'ui-contract.mjs', 'app.mjs']) {
  const content = readFileSync(join(ROOT, 'src', relative), 'utf8');
  if (NETWORK_CODE.test(content)) throw new Error(`network-capable expression found in src/${relative}`);
}

rmSync(DIST, { recursive: true, force: true });
mkdirSync(join(DIST, 'src'), { recursive: true });
for (const relative of SOURCE_FILES) cpSync(join(ROOT, relative), join(DIST, relative));
cpSync(join(ROOT, 'src'), join(DIST, 'src'), { recursive: true });
writeFileSync(join(DIST, 'build-manifest.json'), `${JSON.stringify({
  entry: 'index.html',
  sourceFiles: [
    ...SOURCE_FILES,
    'src/model.mjs',
    'src/solver.mjs',
    'src/iaa.mjs',
    'src/ui-contract.mjs',
    'src/app.mjs',
  ],
  externalAssets: 0,
  networkCapableExpressions: 0,
  throwaway: true,
}, null, 2)}\n`);
process.stdout.write(`built ${DIST}\n`);
