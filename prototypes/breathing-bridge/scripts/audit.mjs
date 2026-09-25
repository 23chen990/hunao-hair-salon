import { mkdir, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { runMachineAudit } from '../src/growing-bridge.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const artifacts = join(root, 'artifacts');
await mkdir(artifacts, { recursive: true });

const startedAt = new Date().toISOString();
const audit = runMachineAudit();
audit.execution = {
  startedAt,
  finishedAt: new Date().toISOString(),
  host: `${process.platform}-${process.arch}`,
  node: process.version,
  command: 'npm run audit',
};

const output = join(artifacts, 'machine-audit.json');
await writeFile(output, `${JSON.stringify(audit, null, 2)}\n`, 'utf8');

console.log(JSON.stringify({
  output,
  overallPass: audit.overallPass,
  hardGates: audit.hardGates,
  solver: `${audit.solver.numerator}/${audit.solver.denominator}`,
  crossFps: `${audit.frameRateReplay.numerator}/${audit.frameRateReplay.denominator}`,
  robustSeeds: `${audit.robustness.numerator}/${audit.robustness.denominator}`,
  random: `${audit.random.numerator}/${audit.random.denominator}`,
  stable: `${audit.stability.numerator}/${audit.stability.denominator}`,
  performanceMs: audit.performance,
}, null, 2));

if (!audit.overallPass) process.exitCode = 1;
