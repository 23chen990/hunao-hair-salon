import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';

import { inspectAsset, normalizeAssetName, processInbox } from '../tools/asset-pipeline.mjs';

const png1x1 = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAFUlEQVR4nGP8z8DQwMDAwMAEIkAYABglAYOd/VRoAAAAAElFTkSuQmCC',
  'base64');

test('normalizes incoming file names to stable kebab case', () => {
  assert.equal(normalizeAssetName('Hair Chair FINAL 02.PNG'), 'hair-chair-final-02.png');
});

test('rejects unsupported formats with a clear error', async () => {
  const dir = await mkdtemp(path.join(tmpdir(), 'hair-salon-assets-'));
  const file = path.join(dir, 'chair.psd');
  await writeFile(file, 'not a psd');
  await assert.rejects(() => inspectAsset(file), /不支持的格式.*\.psd/);
});

test('does not overwrite an approved asset with the same stable id', async () => {
  const root = await mkdtemp(path.join(tmpdir(), 'hair-salon-assets-'));
  await mkdir(path.join(root, 'assets', 'inbox'), { recursive: true });
  await mkdir(path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'Imported'), { recursive: true });
  await mkdir(path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'AssetPipeline'), { recursive: true });
  await writeFile(path.join(root, 'assets', 'inbox', 'chair.png'), png1x1);
  await writeFile(path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'AssetPipeline', 'asset-manifest.json'), JSON.stringify({
    SchemaVersion: 1,
    Assets: [{ Id: 'furniture-chair', Status: 'approved', ResourcePath: 'Imported/chair' }]
  }));

  await assert.rejects(
    () => processInbox({ root, id: 'furniture-chair', type: 'furniture', fileName: 'chair.png' }),
    /已定稿.*禁止覆盖/);
});

test('imports a valid png, creates a preview and updates the manifest', async () => {
  const root = await mkdtemp(path.join(tmpdir(), 'hair-salon-assets-'));
  await mkdir(path.join(root, 'assets', 'inbox'), { recursive: true });
  await mkdir(path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'Imported'), { recursive: true });
  await mkdir(path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'AssetPipeline'), { recursive: true });
  await writeFile(path.join(root, 'assets', 'inbox', 'New Chair.PNG'), png1x1);
  await writeFile(path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'AssetPipeline', 'asset-manifest.json'), JSON.stringify({ SchemaVersion: 1, Assets: [] }));

  const result = await processInbox({ root, id: 'furniture-new-chair', type: 'furniture', fileName: 'New Chair.PNG' });
  const manifest = JSON.parse(await readFile(path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'AssetPipeline', 'asset-manifest.json'), 'utf8'));

  assert.equal(result.normalizedName, 'new-chair.png');
  assert.equal(manifest.Assets[0].Id, 'furniture-new-chair');
  assert.match(result.previewPath, /assets\/previews\/furniture-new-chair\.png$/);
});

test('rolls back copied files and manifest when preview generation fails', async () => {
  const root = await mkdtemp(path.join(tmpdir(), 'hair-salon-assets-'));
  const inboxDir = path.join(root, 'assets', 'inbox');
  const manifestPath = path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'AssetPipeline', 'asset-manifest.json');
  const importedPath = path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'Imported', 'furniture-rollback.png');
  await mkdir(inboxDir, { recursive: true });
  await mkdir(path.dirname(manifestPath), { recursive: true });
  await writeFile(path.join(inboxDir, 'rollback.png'), png1x1);
  await writeFile(manifestPath, JSON.stringify({ SchemaVersion: 1, Assets: [] }));

  await assert.rejects(
    () => processInbox({
      root,
      id: 'furniture-rollback',
      type: 'furniture',
      fileName: 'rollback.png',
      createPreviewFile: async () => { throw new Error('测试夹具：预览生成失败'); }
    }),
    /自动回滚/);
  await assert.rejects(() => readFile(importedPath), /ENOENT/);
  const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
  assert.deepEqual(manifest.Assets, []);
  assert.equal((await readFile(path.join(inboxDir, 'rollback.png'))).length, png1x1.length);
});
