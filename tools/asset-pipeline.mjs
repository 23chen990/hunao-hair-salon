#!/usr/bin/env node
import { execFile } from 'node:child_process';
import { createHash } from 'node:crypto';
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
  const sourceBytes = await readFile(filePath);
  const script = [
    'import json,sys',
    'from PIL import Image',
    'p=sys.argv[1]',
    'im=Image.open(p)',
    'actual=im.format',
    'im.verify()',
    'im=Image.open(p).convert("RGBA")',
    'a=im.getchannel("A")',
    'bbox=a.getbbox()',
    'has_alpha=(a.getextrema()[0] < 255)',
    'threshold=16',
    'effective=a.point(lambda v:255 if v>threshold else 0)',
    'effective_bbox=effective.getbbox()',
    'px=list(im.getdata())',
    'edge=[im.getpixel((x,0)) for x in range(im.width)]+[im.getpixel((x,im.height-1)) for x in range(im.width)]+[im.getpixel((0,y)) for y in range(1,im.height-1)]+[im.getpixel((im.width-1,y)) for y in range(1,im.height-1)]',
    'pollution=sum(1 for r,g,b,aa in px if aa==0 and (r!=0 or g!=0 or b!=0))',
    'edge_effective=sum(1 for r,g,b,aa in edge if aa>threshold)',
    'edge_any=sum(1 for r,g,b,aa in edge if aa>0)',
    'opaque_border=sum(1 for r,g,b,aa in edge if aa>250)',
    'white_border=sum(1 for r,g,b,aa in edge if aa>250 and r>245 and g>245 and b>245)',
    'white_bg=(len(edge)>0 and opaque_border/len(edge)>.92 and white_border/max(1,opaque_border)>.92)',
    'partial=sum(1 for r,g,b,aa in px if 0<aa<255)',
    'payload={"width":im.width,"height":im.height,"actualFormat":actual,"hasAlpha":has_alpha,"bbox":bbox,"effectiveBounds":effective_bbox,"edgePixelsAboveThreshold":edge_effective,"edgePixelsAnyAlpha":edge_any,"transparentEdgeColorPollutionPixels":pollution,"whiteBackgroundLikely":white_bg,"partialAlphaPixels":partial}',
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
  if (result.whiteBackgroundLikely) warnings.push('边缘高度疑似纯白不透明背景，请确认是否为预期面板素材。');
  if (result.transparentEdgeColorPollutionPixels > 0)
    warnings.push(`发现 ${result.transparentEdgeColorPollutionPixels} 个全透明但带颜色的像素，缩放时可能产生透明边缘色污染。`);
  if (result.edgePixelsAboveThreshold > 0) {
    warnings.push('有效可见内容接触图片边缘，可能存在边缘截断；接入正式场景前必须查看预览确认。');
  } else if ([left, top, result.width - right, result.height - bottom].some(margin => margin === 0)) {
    warnings.push('仅极低 Alpha 像素接触图片边缘；有效内容未触边，仍需在透明底预览中确认。');
  }
  return {
    ...result,
    format: result.actualFormat,
    bytes: info.size,
    sha256: createHash('sha256').update(sourceBytes).digest('hex'),
    visibleRatio,
    margins: { left, top, right: result.width - right, bottom: result.height - bottom },
    alphaEdges: {
      threshold: 16,
      effectiveBounds: result.effectiveBounds,
      edgePixelsAboveThreshold: result.edgePixelsAboveThreshold,
      edgePixelsAnyAlpha: result.edgePixelsAnyAlpha
    },
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

function requireNumber(value, label) {
  if (!Number.isFinite(value)) throw new Error(`${label} 必须是有限数字。`);
}

function requireVector(value, label, positive = false) {
  if (!value || typeof value !== 'object') throw new Error(`${label} 缺失。`);
  requireNumber(value.x, `${label}.x`);
  requireNumber(value.y, `${label}.y`);
  if (positive && (value.x <= 0 || value.y <= 0)) throw new Error(`${label} 必须为正数。`);
}

function requireVector3(value, label) {
  if (!value || typeof value !== 'object') throw new Error(`${label} 缺失。`);
  requireNumber(value.x, `${label}.x`);
  requireNumber(value.y, `${label}.y`);
  requireNumber(value.z, `${label}.z`);
}

function validateDefinition(definition) {
  if (!definition || typeof definition !== 'object') throw new Error('缺少明确的资产配置；禁止自动猜测方向、占地、碰撞、锚点或阴影。');
  if (!definition.displayName?.trim()) throw new Error('资产配置缺少 displayName。');
  if (!definition.category?.trim()) throw new Error('资产配置缺少 category。');
  if (definition.status !== 'candidate') throw new Error('本轮新资产必须以 candidate 状态接入。');
  if (!Array.isArray(definition.availableOrientations) || definition.availableOrientations.length === 0)
    throw new Error('资产配置至少需要一个真实存在的 availableOrientation。');
  const allowed = new Set(['back-wall', 'right-wall', 'free']);
  if (definition.availableOrientations.some(item => !allowed.has(item))) throw new Error('资产配置包含非法 orientation。');
  if (!definition.availableOrientations.includes(definition.defaultOrientation))
    throw new Error('defaultOrientation 必须存在于 availableOrientations 中。');
  requireVector(definition.pivot, 'pivot');
  if (definition.pivot.x < 0 || definition.pivot.x > 1 || definition.pivot.y < 0 || definition.pivot.y > 1)
    throw new Error('pivot 必须是 0 到 1 的归一化值。');
  if (definition.importProfile !== 'production-2.5d-rendered')
    throw new Error('2.5D 透明预渲染资产必须使用 importProfile=production-2.5d-rendered。');
  if (!['footprint-isometric', 'explicit'].includes(definition.scalePolicy))
    throw new Error('scalePolicy 必须是 footprint-isometric 或 explicit。');
  if (definition.scalePolicy === 'explicit') requireVector(definition.desiredWorldSize, 'desiredWorldSize', true);
  for (const key of ['footprint', 'collision']) {
    if (!definition[key]) throw new Error(`资产配置缺少 ${key}。`);
    requireVector(definition[key].center, `${key}.center`);
    requireVector(definition[key].size, `${key}.size`, true);
  }
  if (!definition.shadow || !['baked', 'procedural', 'none'].includes(definition.shadow.mode))
    throw new Error('shadow.mode 必须是 baked、procedural 或 none。');
  requireVector(definition.shadow.size, 'shadow.size', true);
  requireVector(definition.shadow.offset, 'shadow.offset');
  requireNumber(definition.shadow.opacity, 'shadow.opacity');
  requireNumber(definition.shadow.softness, 'shadow.softness');
  if (!Array.isArray(definition.interactionAnchors) || !Array.isArray(definition.requiredAnchors))
    throw new Error('资产配置必须明确 interactionAnchors 和 requiredAnchors。');
  if (!definition.sorting?.layer) throw new Error('资产配置缺少 sorting.layer。');
  requireNumber(definition.sorting.order, 'sorting.order');
  requireNumber(definition.sorting.depthOffset, 'sorting.depthOffset');
  const candidate = definition.candidateValidation;
  if (!candidate || !['ordinary', 'service-station'].includes(candidate.role))
    throw new Error('candidateValidation.role 必须是 ordinary 或 service-station。');
  requireVector3(candidate.worldPosition, 'candidateValidation.worldPosition');
  if (!Array.isArray(candidate.hideObjectNames)) throw new Error('candidateValidation.hideObjectNames 必须是数组。');
  requireNumber(candidate.hideRadius, 'candidateValidation.hideRadius');
  if (candidate.hideRadius < 0) throw new Error('candidateValidation.hideRadius 不能小于 0。');
  if (candidate.role === 'service-station') {
    if (!candidate.serviceType?.trim()) throw new Error('交互工位缺少 candidateValidation.serviceType。');
    if (!Number.isInteger(candidate.stationId) || candidate.stationId < 0)
      throw new Error('交互工位 candidateValidation.stationId 必须是非负整数。');
    if (!candidate.targetObjectName?.trim()) throw new Error('交互工位缺少 candidateValidation.targetObjectName。');
  }
}

function vector(value) { return { x: value.x, y: value.y }; }
function area(value) { return { Center: vector(value.center), Size: vector(value.size) }; }

function roundWorld(value) { return Math.round(value * 1_000_000) / 1_000_000; }

function desiredWorldSize(image, definition) {
  if (definition.scalePolicy === 'explicit') return vector(definition.desiredWorldSize);
  const bounds = image.effectiveBounds || image.bbox;
  const visiblePixelWidth = bounds[2] - bounds[0];
  const visibleWorldWidth = definition.footprint.size.x + definition.footprint.size.y * 0.45;
  const fullWidth = visibleWorldWidth * image.width / visiblePixelWidth;
  return { x: roundWorld(fullWidth), y: roundWorld(fullWidth * image.height / image.width) };
}

function configuredEntry({ id, type, resourcePath, image, definition }) {
  validateDefinition(definition);
  const mode = definition.shadow.mode;
  return {
    Id: id,
    DisplayName: definition.displayName,
    Category: definition.category,
    Type: type,
    ResourcePath: resourcePath,
    VisualPath: resourcePath,
    Visuals: definition.availableOrientations.map(orientation => ({ Orientation: orientation, ResourcePath: resourcePath })),
    Status: definition.status,
    Directions: [...definition.availableOrientations],
    DefaultOrientation: definition.defaultOrientation,
    Pivot: vector(definition.pivot),
    DesiredWorldSize: desiredWorldSize(image, definition),
    ScalePolicy: definition.scalePolicy,
    ImportProfile: definition.importProfile,
    Footprint: area(definition.footprint),
    Collision: area(definition.collision),
    Shadow: {
      Mode: mode,
      Enabled: mode === 'procedural',
      Size: vector(definition.shadow.size),
      Offset: vector(definition.shadow.offset),
      Opacity: definition.shadow.opacity,
      Softness: definition.shadow.softness
    },
    InteractionAnchors: definition.interactionAnchors.map(anchor => ({
      Id: anchor.id,
      Position: { x: anchor.position.x, y: anchor.position.y, z: anchor.position.z },
      Facing: { x: anchor.facing.x, y: anchor.facing.y, z: anchor.facing.z }
    })),
    RequiredAnchors: [...definition.requiredAnchors],
    Sorting: {
      Layer: definition.sorting.layer,
      Order: definition.sorting.order,
      DepthOffset: definition.sorting.depthOffset,
      SortByWorldZ: definition.sorting.sortByWorldZ !== false
    },
    CandidateValidation: {
      Role: definition.candidateValidation.role,
      ServiceType: definition.candidateValidation.serviceType || 'none',
      StationId: definition.candidateValidation.stationId ?? -1,
      TargetObjectName: definition.candidateValidation.targetObjectName || '',
      WorldPosition: {
        x: definition.candidateValidation.worldPosition.x,
        y: definition.candidateValidation.worldPosition.y,
        z: definition.candidateValidation.worldPosition.z
      },
      HideObjectNames: [...definition.candidateValidation.hideObjectNames],
      HideRadius: definition.candidateValidation.hideRadius
    },
    SourceHash: image.sha256,
    Source: {
      OriginalPath: definition.sourceOriginalPath || '',
      Width: image.width,
      Height: image.height,
      Bytes: image.bytes,
      HasAlpha: image.hasAlpha,
      VisibleBounds: image.bbox,
      EffectiveBounds: image.effectiveBounds
    }
  };
}

export async function processInbox({ root, id, type, fileName, definition, createPreviewFile = createPreview }) {
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
  validateDefinition(definition);
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
    manifest.Assets.push(configuredEntry({ id, type, resourcePath, image, definition }));
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
  const configArg = value('--config');
  if (!configArg) throw new Error('import 必须提供 --config <资产配置 JSON>，禁止自动猜测生产参数。');
  const configPath = path.resolve(root, configArg);
  const definition = JSON.parse(await readFile(configPath, 'utf8'));
  const result = await processInbox({ root, fileName: value('--file'), id: value('--id'), type: value('--type'), definition });
  process.stdout.write(`接入成功：${result.id}\n资源：${result.importedPath}\n预览：${result.previewPath}\n原文件归档：${result.archivedPath}\n`);
  for (const warning of result.image.warnings) process.stdout.write(`提醒：${warning}\n`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main().catch(error => {
    process.stderr.write(`资产接入失败：${error.message}\n`);
    process.exitCode = 1;
  });
}
