import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

import { generateAcceptedLevel } from '../src/generator.ts';
import { ACCEPTANCE_SEEDS } from '../src/levels.ts';
import { DEFAULT_RULES, simulateAttempt } from '../src/rules.ts';
import { DEFAULT_SOLVER_OPTIONS, auditSeeds, sampleSolutions } from '../src/solver.ts';

const started = performance.now();
const acceptanceSeeds = ACCEPTANCE_SEEDS.map((seed) => {
  const level = generateAcceptedLevel(seed);
  const sample = sampleSolutions(level);
  const reference = simulateAttempt(level, level.referenceInput);
  return {
    seed,
    generationAttempt: level.generationAttempt,
    pieces: level.pieces.length,
    referenceOutcome: reference.outcome,
    referenceSpreadSeconds: reference.arrivalSpread,
    sampledSolutions: sample.successCount,
    sampledInputs: sample.totalSamples,
    randomSuccessRate: sample.successRate,
    largestComponent: sample.largestComponent,
    spatialNeighbor: sample.hasSpatialNeighbor,
    timeNeighbor: sample.hasTimeNeighbor,
  };
});
const audit = auditSeeds(200);
const report = {
  generatedAt: new Date().toISOString(),
  elapsedMilliseconds: Math.round(performance.now() - started),
  rules: DEFAULT_RULES,
  solverSampling: DEFAULT_SOLVER_OPTIONS,
  acceptanceSeeds,
  audit,
};

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const output = resolve(root, 'reports', 'validation-report.json');
await mkdir(dirname(output), { recursive: true });
await writeFile(output, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
console.log(JSON.stringify(report, null, 2));
console.log(`Wrote ${output}`);
