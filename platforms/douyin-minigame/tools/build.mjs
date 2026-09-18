import { copyFile, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { basename, dirname, extname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { runtimeImports, transformToCommonJs } from './build-lib.mjs';

const platformRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const workspaceRoot = resolve(platformRoot, '../..');
const sourceRoot = join(platformRoot, 'src');
const outputRoot = join(platformRoot, 'dist');
const outputScriptRoot = join(outputRoot, 'js');
const assetSourceRoot = join(workspaceRoot, 'prototypes/phase-ripple-skinned/assets');

function outputName(sourcePath) {
  return `${basename(sourcePath, extname(sourcePath))}.js`;
}

function resolveImport(sourcePath, specifier) {
  if (!specifier.startsWith('.')) throw new Error(`Only local imports are supported: ${specifier}`);
  return resolve(dirname(sourcePath), specifier);
}

export async function buildDouyinPackage() {
  await rm(outputRoot, { recursive: true, force: true });
  await mkdir(outputScriptRoot, { recursive: true });
  await mkdir(join(outputRoot, 'assets'), { recursive: true });

  const entryPaths = [join(sourceRoot, 'launch.ts'), join(sourceRoot, 'douyin-app.ts')];
  const queue = [...entryPaths];
  const visited = new Set();
  const outputOwners = new Map();

  while (queue.length > 0) {
    const sourcePath = queue.shift();
    if (visited.has(sourcePath)) continue;
    visited.add(sourcePath);
    const source = await readFile(sourcePath, 'utf8');
    const name = outputName(sourcePath);
    const existingOwner = outputOwners.get(name);
    if (existingOwner !== undefined && existingOwner !== sourcePath) {
      throw new Error(`Duplicate output module name ${name}`);
    }
    outputOwners.set(name, sourcePath);

    for (const specifier of runtimeImports(source)) queue.push(resolveImport(sourcePath, specifier));
    const transformed = transformToCommonJs(source, (specifier) =>
      `./${outputName(resolveImport(sourcePath, specifier))}`);
    await writeFile(join(outputScriptRoot, name), transformed, 'utf8');
  }

  await writeFile(join(outputRoot, 'game.js'), [
    "require('./js/launch.js');",
    "require('./js/douyin-app.js');",
    '',
  ].join('\n'), 'utf8');
  await writeFile(join(outputRoot, 'game.json'), `${JSON.stringify({
    deviceOrientation: 'portrait',
    showStatusBar: false,
    menuButtonStyle: 'light',
  }, null, 2)}\n`, 'utf8');
  await writeFile(join(outputRoot, 'project.config.json'), `${JSON.stringify({
    appid: 'REPLACE_WITH_DOUYIN_APPID',
    projectname: 'phase-ripple',
    setting: {
      es6: true,
      minified: false,
      urlCheck: true,
    },
  }, null, 2)}\n`, 'utf8');

  for (const asset of ['tabletop-board.png', 'piece-outbound.png', 'piece-returned.png']) {
    await copyFile(join(assetSourceRoot, asset), join(outputRoot, 'assets', asset));
  }

  return { moduleCount: visited.size, outputRoot };
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const result = await buildDouyinPackage();
  console.log(`Built ${result.moduleCount} CommonJS modules into ${result.outputRoot}`);
}
