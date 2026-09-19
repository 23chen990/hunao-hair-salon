import { createHash } from 'node:crypto';
import { cp, mkdir, readFile, readdir, rm, stat, writeFile } from 'node:fs/promises';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildDouyinPackage } from './build.mjs';

const platformRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const workspaceRoot = resolve(platformRoot, '../..');
const artifactRoot = join(workspaceRoot, 'artifacts/phase-ripple-douyin-rc');
const packageRoot = join(artifactRoot, 'package');

async function collect(directory) {
  const files = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) files.push(...await collect(path));
    else files.push(path);
  }
  return files;
}

await buildDouyinPackage();
await rm(artifactRoot, { recursive: true, force: true });
await mkdir(artifactRoot, { recursive: true });
await cp(join(platformRoot, 'dist'), packageRoot, { recursive: true });
await cp(join(platformRoot, 'RELEASE_CHECKLIST.md'), join(artifactRoot, 'RELEASE_CHECKLIST.md'));

const records = [];
let packageBytes = 0;
for (const path of await collect(packageRoot)) {
  const content = await readFile(path);
  const size = (await stat(path)).size;
  packageBytes += size;
  records.push({
    path: relative(packageRoot, path),
    size,
    sha256: createHash('sha256').update(content).digest('hex'),
  });
}
records.sort((left, right) => left.path.localeCompare(right.path));

const manifest = {
  generatedAt: new Date().toISOString(),
  product: '相位涟漪（暂名）',
  artifactKind: 'douyin-developer-tools-import-test',
  releaseStatus: 'blocked_on_real_appid_credentials_and_device_review',
  appIdPlaceholder: true,
  monetizationIntegrated: false,
  packageBytes,
  packageLimitBytes: 20 * 1024 * 1024,
  files: records,
};
await writeFile(join(artifactRoot, 'manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
console.log(JSON.stringify({ artifactRoot, packageBytes, fileCount: records.length }, null, 2));
