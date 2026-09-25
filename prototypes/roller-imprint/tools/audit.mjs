import { mkdirSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join, resolve } from 'node:path';
import { runAudit } from '../src/audit.mjs';

const ROOT = resolve(fileURLToPath(new URL('..', import.meta.url)));
const artifacts = join(ROOT, 'artifacts');
const browserArtifact = join(artifacts, 'browser-smoke.json');
const result = runAudit({
  seedCount: 500,
  randomTrials: 10000,
  browserArtifactPath: browserArtifact,
});
mkdirSync(artifacts, { recursive: true });
writeFileSync(join(artifacts, 'machine-audit.json'), `${JSON.stringify(result, null, 2)}\n`);
process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
if (!result.implementationGatesPass) process.exitCode = 1;
