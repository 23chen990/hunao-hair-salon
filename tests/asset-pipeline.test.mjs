import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, stat, writeFile } from 'node:fs/promises';
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

  const definition = {
    displayName: 'New Chair',
    category: 'chair',
    status: 'candidate',
    availableOrientations: ['right-wall'],
    defaultOrientation: 'right-wall',
    pivot: { x: 0.5, y: 0 },
    importProfile: 'production-2.5d-rendered',
    scalePolicy: 'footprint-isometric',
    footprint: { center: { x: 0, y: 0 }, size: { x: 2.4, y: 2.8 } },
    collision: { center: { x: 0, y: 0.1 }, size: { x: 2, y: 2.2 } },
    shadow: { mode: 'baked', size: { x: 2.4, y: 2.8 }, offset: { x: 0, y: 0 }, opacity: 0, softness: 0.8 },
    interactionAnchors: [],
    requiredAnchors: [],
    sorting: { layer: 'Default', order: 4, depthOffset: 0, sortByWorldZ: true },
    candidateValidation: {
      role: 'service-station', serviceType: 'wash', stationId: 0,
      targetObjectName: 'Wash Workstation 1', worldPosition: { x: 9, y: 0, z: -0.3 },
      hideObjectNames: ['Plant Pot', 'Faceted Leaf'], hideRadius: 2.2
    },
    sourceOriginalPath: '/approved/New Chair.PNG'
  };
  const result = await processInbox({ root, id: 'furniture-new-chair', type: 'furniture', fileName: 'New Chair.PNG', definition });
  const manifest = JSON.parse(await readFile(path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'AssetPipeline', 'asset-manifest.json'), 'utf8'));

  assert.equal(result.normalizedName, 'new-chair.png');
  assert.equal(manifest.Assets[0].Id, 'furniture-new-chair');
  assert.equal(manifest.Assets[0].Status, 'candidate');
  assert.deepEqual(manifest.Assets[0].Directions, ['right-wall']);
  assert.equal(manifest.Assets[0].DefaultOrientation, 'right-wall');
  assert.equal(manifest.Assets[0].Shadow.Mode, 'baked');
  assert.equal(manifest.Assets[0].Shadow.Enabled, false);
  assert.equal(manifest.Assets[0].SourceHash, result.image.sha256);
  assert.equal(manifest.Assets[0].Source.OriginalPath, '/approved/New Chair.PNG');
  assert.equal(manifest.Assets[0].ImportProfile, 'production-2.5d-rendered');
  assert.equal(manifest.Assets[0].ScalePolicy, 'footprint-isometric');
  assert.ok(manifest.Assets[0].DesiredWorldSize.x > 0);
  assert.ok(manifest.Assets[0].DesiredWorldSize.y > 0);
  assert.equal(Object.hasOwn(manifest.Assets[0], 'VisualSize'), false);
  assert.deepEqual(manifest.Assets[0].Visuals, [
    { Orientation: 'right-wall', ResourcePath: 'Imported/furniture-new-chair' }
  ]);
  assert.equal(manifest.Assets[0].CandidateValidation.Role, 'service-station');
  assert.deepEqual(manifest.Assets[0].CandidateValidation.WorldPosition, { x: 9, y: 0, z: -0.3 });
  assert.match(result.previewPath, /assets\/previews\/furniture-new-chair\.png$/);
});

test('inspection records production fingerprint and effective alpha-edge evidence', async () => {
  const dir = await mkdtemp(path.join(tmpdir(), 'hair-salon-assets-'));
  const file = path.join(dir, 'edge.png');
  await writeFile(file, png1x1);

  const inspected = await inspectAsset(file);

  assert.match(inspected.sha256, /^[a-f0-9]{64}$/);
  assert.equal(inspected.bytes, (await stat(file)).size);
  assert.equal(inspected.actualFormat, 'PNG');
  assert.ok(inspected.alphaEdges);
  assert.ok(inspected.alphaEdges.effectiveBounds);
  assert.equal(typeof inspected.alphaEdges.edgePixelsAboveThreshold, 'number');
  assert.equal(typeof inspected.transparentEdgeColorPollutionPixels, 'number');
  assert.equal(typeof inspected.whiteBackgroundLikely, 'boolean');
});

test('requires an explicit production definition instead of inventing furniture directions', async () => {
  const root = await mkdtemp(path.join(tmpdir(), 'hair-salon-assets-'));
  await mkdir(path.join(root, 'assets', 'inbox'), { recursive: true });
  await mkdir(path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'AssetPipeline'), { recursive: true });
  await writeFile(path.join(root, 'assets', 'inbox', 'chair.png'), png1x1);
  await writeFile(path.join(root, 'unity-hair-salon', 'Assets', 'Resources', 'AssetPipeline', 'asset-manifest.json'), JSON.stringify({ SchemaVersion: 1, Assets: [] }));

  await assert.rejects(
    () => processInbox({ root, id: 'furniture-no-guesses', type: 'furniture', fileName: 'chair.png' }),
    /缺少明确的资产配置/);
});

test('browser QA exposes a candidate scene mode with alignment, active-service and completion evidence', async () => {
  const source = await readFile(new URL('../tools/browser-check.py', import.meta.url), 'utf8');
  assert.match(source, /"candidate"/);
  assert.match(source, /CANDIDATE_ALIGNMENT_READY/);
  assert.match(source, /CANDIDATE_SERVICE_ACTIVE/);
  assert.match(source, /CANDIDATE_FLOW_PASS/);
  assert.match(source, /WebGLCandidate/);
});

test('full project gate rebuilds the candidate and validates fresh Unity XML', async () => {
  const source = await readFile(new URL('../tools/check-project.sh', import.meta.url), 'utf8');
  assert.match(source, /BuildScript\.BuildWebGLCandidate/);
  assert.match(source, /PipelineEditMode\.xml/);
  assert.match(source, /ElementTree|xml\.etree/);
});

test('visual QA captures source, inspect, context and 100-percent pixel evidence', async () => {
  const source = await readFile(new URL('../tools/browser-check.py', import.meta.url), 'utf8');
  assert.match(source, /source-preview/);
  assert.match(source, /view=inspect/);
  assert.match(source, /view=context/);
  assert.match(source, /view=pixel/);
  assert.match(source, /devicePixelRatio/);
});

test('ingest skill can revalidate an existing candidate without copying or replacing its PNG', async () => {
  const source = await readFile(new URL('../.agents/skills/salon-asset-ingest/scripts/ingest.sh', import.meta.url), 'utf8');
  assert.match(source, /--revalidate/);
  assert.match(source, /revalidate_mode/);
  assert.match(source, /if \[\[ "\$revalidate_mode" != "true" \]\]/);
  assert.match(source, /browser-check\.py.*--asset-id/);
});

test('visual QA preserves mode-specific machine and report evidence across successive runs', async () => {
  const browser = await readFile(new URL('../tools/browser-check.py', import.meta.url), 'utf8');
  const writer = await readFile(new URL('../.agents/skills/salon-visual-qa/scripts/write_report.py', import.meta.url), 'utf8');
  assert.match(browser, /browser-check-\{args\.mode\}\.json/);
  assert.match(writer, /browser-check-\{MODE\}\.json/);
  assert.match(writer, /visual-qa-report-\{MODE\}\.json/);
});

test('visual QA can render a generic fixed-camera context scale comparison', async () => {
  const source = await readFile(new URL('../tools/browser-check.py', import.meta.url), 'utf8');
  assert.match(source, /--context-scale-compare/);
  assert.match(source, /--context-scale-values/);
  assert.match(source, /contextScale=/);
  assert.match(source, /context-scale-comparison/);
});

test('visual QA supports an independent reference scene and approved side-by-side evidence', async () => {
  const browser = await readFile(new URL('../tools/browser-check.py', import.meta.url), 'utf8');
  const runner = await readFile(new URL('../.agents/skills/salon-visual-qa/scripts/run.sh', import.meta.url), 'utf8');
  assert.match(browser, /"reference"/);
  assert.match(browser, /REFERENCE_VISUAL_READY/);
  assert.match(browser, /reference-visual-side-by-side/);
  assert.match(runner, /BuildScript\.BuildWebGLReferenceVisual/);
});

test('reference visual QA captures overlay calibration evidence and relative screen metrics', async () => {
  const browser = await readFile(new URL('../tools/browser-check.py', import.meta.url), 'utf8');
  assert.match(browser, /reference-visual-approved-reference/);
  assert.match(browser, /reference-visual-old-round2-2/);
  assert.match(browser, /reference-visual-new-calibrated/);
  assert.match(browser, /reference-visual-overlay-calibration/);
  assert.match(browser, /overlayOpacity=/);
  assert.match(browser, /target-density/);
  assert.match(browser, /screenMetrics/);
  assert.match(browser, /magenta shader fallback/);
  assert.match(browser, /assert_reference_metric_alignment/);
});

test('formal demo visual QA owns the wash-area A-B-C-D evidence instead of the reference scene', async () => {
  const browser = await readFile(new URL('../tools/browser-check.py', import.meta.url), 'utf8');
  const skill = await readFile(new URL('../.agents/skills/salon-visual-qa/SKILL.md', import.meta.url), 'utf8');
  const writer = await readFile(new URL('../.agents/skills/salon-visual-qa/scripts/write_report.py', import.meta.url), 'utf8');
  assert.match(browser, /wash-area-approved-reference-crop-844x390/);
  assert.match(browser, /wash-area-demo-before-844x390/);
  assert.match(browser, /wash-area-demo-after-844x390/);
  assert.match(browser, /wash-area-player-context-844x390/);
  assert.match(browser, /WASH_AREA_VISUAL_READY/);
  assert.match(browser, /washAreaVisual=before/);
  assert.match(browser, /washAreaVisual=after/);
  assert.match(browser, /assert_no_day_transition_overlay/);
  assert.match(browser, /washAreaVisual=after&browserSmoke=1/);
  assert.match(skill, /HairSalonDemo.*唯一正式 Visual Integration Scene/s);
  assert.match(skill, /Reference Scene.*技术检查/s);
  assert.match(writer, /washAreaEvidence/);
  assert.match(writer, /A_approvedReferenceCrop/);
});

test('formal demo visual QA captures the opening card and active service toolbar at the ship viewport', async () => {
  const browser = await readFile(new URL('../tools/browser-check.py', import.meta.url), 'utf8');
  const smoke = await readFile(new URL('../unity-hair-salon/Assets/Scripts/AssetPipeline/BrowserCoreFlowSmoke.cs', import.meta.url), 'utf8');
  const writer = await readFile(new URL('../.agents/skills/salon-visual-qa/scripts/write_report.py', import.meta.url), 'utf8');
  assert.match(browser, /demo-ui-opening-844x390/);
  assert.match(browser, /demo-ui-active-service-844x390/);
  assert.match(browser, /uiEvidence=service/);
  assert.match(browser, /UI_SERVICE_READY/);
  assert.match(smoke, /uiEvidence=service/);
  assert.match(smoke, /UI_SERVICE_READY/);
  assert.match(writer, /uiEvidence/);
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
      definition: {
        displayName: 'Rollback', category: 'test', status: 'candidate',
        availableOrientations: ['free'], defaultOrientation: 'free',
        pivot: { x: 0.5, y: 0 }, importProfile: 'production-2.5d-rendered', scalePolicy: 'footprint-isometric',
        footprint: { center: { x: 0, y: 0 }, size: { x: 1, y: 1 } },
        collision: { center: { x: 0, y: 0 }, size: { x: 1, y: 1 } },
        shadow: { mode: 'none', size: { x: 1, y: 1 }, offset: { x: 0, y: 0 }, opacity: 0, softness: 0 },
        interactionAnchors: [], requiredAnchors: [],
        sorting: { layer: 'Default', order: 0, depthOffset: 0, sortByWorldZ: true },
        candidateValidation: {
          role: 'ordinary', serviceType: 'none', stationId: -1, targetObjectName: '',
          worldPosition: { x: 0, y: 0, z: 0 }, hideObjectNames: [], hideRadius: 0
        }
      },
      createPreviewFile: async () => { throw new Error('测试夹具：预览生成失败'); }
    }),
    /自动回滚/);
  await assert.rejects(() => readFile(importedPath), /ENOENT/);
  const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
  assert.deepEqual(manifest.Assets, []);
  assert.equal((await readFile(path.join(inboxDir, 'rollback.png'))).length, png1x1.length);
});
