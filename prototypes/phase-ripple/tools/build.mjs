import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import { dirname, extname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { stripTypeScriptTypes } from 'node:module';

const prototypeRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const sourceRoot = join(prototypeRoot, 'src');
const outputRoot = join(prototypeRoot, 'dist');

async function collect(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) files.push(...await collect(path));
    else if (extname(entry.name) === '.ts') files.push(path);
  }
  return files;
}

const sources = await collect(sourceRoot);
for (const sourcePath of sources) {
  const relativePath = relative(sourceRoot, sourcePath).replace(/\.ts$/, '.js');
  const outputPath = join(outputRoot, relativePath);
  const source = await readFile(sourcePath, 'utf8');
  const stripped = stripTypeScriptTypes(source, { mode: 'strip' })
    .replace(/\.ts(['"])/g, '.js$1');
  if (/from\s+['"][^'"]+\.ts['"]/.test(stripped)) {
    throw new Error(`Unrewritten TypeScript import in ${relativePath}`);
  }
  await mkdir(dirname(outputPath), { recursive: true });
  await writeFile(outputPath, `${stripped.trimEnd()}\n`, 'utf8');
}

console.log(`Built ${sources.length} TypeScript modules into dist/ (Node native type stripping).`);
