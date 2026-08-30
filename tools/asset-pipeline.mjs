#!/usr/bin/env node
import { execFile } from 'node:child_process';
import { copyFile, mkdir, readFile, rename, rm, stat, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);
const supportedExtensions = new Set(['.png', '.jpg', '.jpeg', '.webp']);

export function normalizeAssetName(fileName) {
  const extension = path.extname(fileName).toLowerCase();
  const base = path.basename(fileName, path.extname(fileName))
    .normalize('NFKD')
    .replace(/[^a-zA-Z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .toLowerCase();
  if (!base) throw new Error(`文件名无法生成稳定名称：${fileName}`);
  return `${base}${extension}`;
}

export async function inspectAsset(filePath) {
  const extension = path.extname(filePath).toLowerCase();
  if (!supportedExtensions.has(extension)) {
    throw new Error(`不支持的格式 ${extension || '（无扩展名）'}；只允许 PNG、JPG/JPEG、WEBP。`);
  }
  const info = await stat(filePath).catch(() => null);
  if (!info?.isFile()) throw new Error(`找不到待接入文件：${filePath}`);
  const script = [
    'import json,sys',
    'from PIL import Image',
    'p=sys.argv[1]',
    'im=Image.open(p)',
    'im.verify()',
    'im=Image.open(p).convert("RGBA")',
    'a=im.getchannel("A")',
    'bbox=a.getbbox()',
    'has_alpha=(a.getextrema()[0] < 255)',
    'payload={"width":im.width,"height":im.height,"format":Image.open(p).format,"hasAlpha":has_alpha,"bbox":bbox}',
    'print(json.dumps(payload))'
  ].join(';');
  let stdout;
  try {
    ({ stdout } = await execFileAsync('python3', ['-c', script, filePath]));
  } catch (error) {
    throw new Error(`图片无法读取或已损坏：${filePath}\n${error.stderr || error.message}`);
  }
  const result = JSON.parse(stdout);
  if (!result.bbox) throw new Error(`图片完全透明，没有可见内容：${filePath}`);
  const [left, top, right, bottom] = result.bbox;
  const visibleRatio = ((right - left) * (bottom - top)) / (result.width * result.height);
  if (visibleRatio < 0.02) throw new Error(`图片可见内容不足 2%，疑似存在异常空白：${filePath}`);
  const warnings = result.hasAlpha ? [] : ['图片没有透明像素；若用于家具或角色，请确认背景是否已正确移除。'];
  if ([left, top, result.width - right, result.height - bottom].some(margin => margin === 0)) {
    warnings.push('可见内容接触图片边缘，可能存在边缘截断；接入正式场景前必须查看预览确认。');
  }
  return {
    ...result,
    visibleRatio,
    margins: { left, top, right: result.width - right, bottom: result.height - bottom },
    warnings
  };
}

async function createPreview(sourcePath, previewPath) {
  const script = [
    'import sys',
    'from PIL import Image,ImageDraw',
    'src,dst=sys.argv[1],sys.argv[2]',
    'size=512',
    'canvas=Image.new("RGBA",(size,size),(242,239,232,255))',
    'draw=ImageDraw.Draw(canvas)',
    'step=32',
    '[(draw.rectangle((x,y,x+step-1,y+step-1),fill=(220,224,225,255)) if ((x//step+y//step)%2) else None) for y in range(0,size,step) for x in range(0,size,step)]',
    'im=Image.open(src).convert("RGBA")',
    'im.thumbnail((448,448),Image.Resampling.LANCZOS)',
    'canvas.alpha_composite(im,((size-im.width)//2,(size-im.height)//2))',
    'canvas.save(dst,"PNG")'
  ].join(';');
  await execFileAsync('python3', ['-c', script, sourcePath, previewPath]);
}

function defaultEntry({ id, type, resourcePath, image }) {
  return {
    Id: id,
    Type: type,
    ResourcePath: resourcePath,
    Status: 'draft',
    Directions: type === 'furniture' ? ['back-wall', 'right-wall'] : ['free'],
    Pivot: { x: 0.5, y: 0 },
    Footprint: { Center: { x: 0, y: 0 }, Size: { x: 1, y: 1 } },
    Collision: { Center: { x: 0, y: 0 }, Size: { x: 1, y: 1 } },
    Shadow: { Enabled: type === 'furniture', Size: { x: 1, y: 0.6 }, Offset: { x: 0, y: 0 }, Opacity: 0.25, Softness: 0.75 },
    InteractionAnchors: [],
    RequiredAnchors: [],
    Sorting: { Layer: 'Default', Order: 0, DepthOffset: 0, SortByWorldZ: true },
    Source: { Width: image.width, Height: image.height, HasAlpha: image.hasAlpha }
  };
}

export async function processInbox({ root, id, type, fileName, createPreviewFile = createPreview }) {
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(id || ''))
    throw new Error(`资产 ID 必须使用小写 kebab-case：${id || '（空）'}`);
  if (!['furniture', 'character', 'ui-sprite', 'effect', 'prop'].includes(type))
    throw new Error(`非法资产类型：${type}`);
  const inboxPath = path.join(root, 'assets', 'inbox', fileName);
  const normalizedName = normalizeAssetName(fileName);
  const manifestPath = path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'AssetPipeline', 'asset-manifest.json');
  const originalManifest = await readFile(manifestPath, 'utf8');
  const manifest = JSON.parse(originalManifest);
  manifest.Assets ??= [];
  const existing = manifest.Assets.find(asset => asset.Id === id);
  if (existing?.Status === 'approved') throw new Error(`资产 ${id} 已定稿，禁止覆盖；请使用新的稳定 ID。`);
  if (existing) throw new Error(`资产 ${id} 已存在且状态为 ${existing.Status || 'unknown'}；请先人工确认是否废弃旧版本。`);
  const image = await inspectAsset(inboxPath);

  const extension = path.extname(normalizedName);
  const targetName = `${id}${extension}`;
  const importedPath = path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'Imported', targetName);
  const previewPath = path.join(root, 'assets', 'previews', `${id}.png`);
  const archiveDir = path.join(root, 'assets', 'archive');
  if (await stat(importedPath).catch(() => null)) throw new Error(`目标资源已存在，禁止覆盖：${importedPath}`);
  if (await stat(previewPath).catch(() => null)) throw new Error(`目标预览已存在，禁止覆盖：${previewPath}`);
  await mkdir(path.dirname(importedPath), { recursive: true });
  await mkdir(path.dirname(previewPath), { recursive: true });
  await mkdir(archiveDir, { recursive: true });
  const archivedPath = path.join(archiveDir, `${Date.now()}-${normalizedName}`);
  let archived = false;
  try {
    await copyFile(inboxPath, importedPath);
    await createPreviewFile(inboxPath, previewPath);
    const resourcePath = `Imported/${id}`;
    manifest.Assets.push(defaultEntry({ id, type, resourcePath, image }));
    manifest.Assets.sort((a, b) => a.Id.localeCompare(b.Id));
    await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
    await rename(inboxPath, archivedPath);
    archived = true;
  } catch (error) {
    const rollbackErrors = [];
    if (archived) await rename(archivedPath, inboxPath).catch(rollbackError => rollbackErrors.push(rollbackError.message));
    await writeFile(manifestPath, originalManifest, 'utf8').catch(rollbackError => rollbackErrors.push(rollbackError.message));
    await rm(previewPath, { force: true }).catch(rollbackError => rollbackErrors.push(rollbackError.message));
    await rm(importedPath, { force: true }).catch(rollbackError => rollbackErrors.push(rollbackError.message));
    const detail = rollbackErrors.length ? `；回滚异常：${rollbackErrors.join('；')}` : '';
    throw new Error(`接入失败，已自动回滚：${error.message}${detail}`);
  }
  return { id, normalizedName, importedPath, previewPath, archivedPath, image };
}

async function main() {
  const args = process.argv.slice(2);
  const value = name => {
    const index = args.indexOf(name);
    return index >= 0 ? args[index + 1] : undefined;
  };
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
  if (args[0] === 'inspect') {
    const requested = value('--file');
    if (!requested) throw new Error('用法：node tools/asset-pipeline.mjs inspect --file <PNG/JPG/WEBP 路径>');
    const filePath = path.resolve(root, requested);
    const result = await inspectAsset(filePath);
    process.stdout.write(`${JSON.stringify({ file: filePath, normalizedName: normalizeAssetName(path.basename(filePath)), ...result }, null, 2)}\n`);
    return;
  }
  if (args[0] !== 'import') {
    throw new Error('用法：node tools/asset-pipeline.mjs import --file <inbox文件名> --id <稳定ID> --type <类型>；或 inspect --file <路径>');
  }
  const result = await processInbox({ root, fileName: value('--file'), id: value('--id'), type: value('--type') });
  process.stdout.write(`接入成功：${result.id}\n资源：${result.importedPath}\n预览：${result.previewPath}\n原文件归档：${result.archivedPath}\n`);
  for (const warning of result.image.warnings) process.stdout.write(`提醒：${warning}\n`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main().catch(error => {
    process.stderr.write(`资产接入失败：${error.message}\n`);
    process.exitCode = 1;
  });
}
