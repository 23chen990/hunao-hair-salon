import { copyFile, mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import { extname, relative, resolve } from 'node:path';

const root = resolve(import.meta.dirname, '..');
const sourceRoot = resolve(root, 'src');
const outputRoot = resolve(root, 'dist');

async function collect(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const path = resolve(directory, entry.name);
    if (entry.isDirectory()) files.push(...await collect(path));
    else if (extname(entry.name) === '.js') files.push(path);
  }
  return files;
}

const sources = await collect(sourceRoot);
for (const source of sources) {
  const destination = resolve(outputRoot, relative(sourceRoot, source));
  await mkdir(resolve(destination, '..'), { recursive: true });
  await copyFile(source, destination);
}

const browserBundleOrder = [
  'prng.js',
  'model.js',
  'input.js',
  'layout.js',
  'eligibility.js',
  'solver.js',
  'app.js',
];
const bundleParts = [];
for (const filename of browserBundleOrder) {
  const source = await readFile(resolve(sourceRoot, filename), 'utf8');
  const classicSource = source
    .replace(/^\s*import\s+[\s\S]*?\s+from\s+['"][^'"]+['"];\s*/gm, '')
    .replace(/^export\s+/gm, '');
  bundleParts.push(`// ${filename}\n${classicSource.trim()}`);
}
const bundle = `(() => {\n'use strict';\n${bundleParts.join('\n\n')}\n})();\n`;
await writeFile(resolve(outputRoot, 'app.bundle.js'), bundle, 'utf8');

process.stdout.write(`Built ${sources.length} throwaway modules and direct-open app.bundle.js into dist/.\n`);
