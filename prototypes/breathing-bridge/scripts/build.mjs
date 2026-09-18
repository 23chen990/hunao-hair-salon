import { createHash } from 'node:crypto';
import { cp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const dist = join(root, 'dist');
const sources = ['index.html', 'styles.css', 'src/browser.mjs', 'src/sim.mjs', 'src/growing-bridge.mjs'];

await rm(dist, { recursive: true, force: true });
await mkdir(join(dist, 'src'), { recursive: true });

const files = [];
for (const source of sources) {
  const from = join(root, source);
  const to = join(dist, source);
  await mkdir(dirname(to), { recursive: true });
  await cp(from, to);
  const bytes = await readFile(to);
  files.push({
    path: relative(dist, to),
    bytes: bytes.length,
    sha256: createHash('sha256').update(bytes).digest('hex'),
  });
}

const manifest = {
  schemaVersion: 1,
  prototype: 'breathing-bridge',
  dependencyCount: 0,
  adsEnabled: false,
  networkAllowed: false,
  files,
};
await writeFile(join(dist, 'build-manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
console.log(JSON.stringify({ output: dist, ...manifest }, null, 2));
