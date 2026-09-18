import { rewardEligibility } from './eligibility.js';
import {
  attemptDrop,
  createGameState,
  dropBead,
  generateLevel,
  moveGameState,
  retryLevel,
} from './model.js';
import { createRng } from './prng.js';
import { solveLevel } from './solver.js';

function percentile(sortedValues, proportion) {
  const index = Math.max(0, Math.ceil(sortedValues.length * proportion) - 1);
  return sortedValues[index];
}

export function auditDurations(count, baseSeed) {
  const durations = [];
  for (let index = 0; index < count; index += 1) {
    const level = generateLevel(baseSeed + index);
    const distance = solveLevel(level).distance;
    const rng = createRng(`duration:${baseSeed + index}`);
    let durationMs = 4_000;
    for (let move = 0; move < distance; move += 1) {
      durationMs += 5_000 + rng.next() * 7_000;
      durationMs += 800 + rng.next() * 700;
    }
    durations.push(Math.round(durationMs));
  }
  durations.sort((a, b) => a - b);
  return {
    samples: count,
    p10Ms: percentile(durations, 0.1),
    medianMs: percentile(durations, 0.5),
    p95Ms: percentile(durations, 0.95),
  };
}

export function auditRandomSuccess(samples, baseSeed) {
  const rng = createRng(`random-layouts:${baseSeed}`);
  let successes = 0;
  for (let index = 0; index < samples; index += 1) {
    const level = generateLevel(baseSeed + (index % 500));
    const offsets = Array.from(
      { length: level.layerCount },
      () => rng.int(level.slotMin, level.slotMax),
    );
    if (dropBead(level, offsets).passed) successes += 1;
  }
  return { samples, successes, rate: successes / samples };
}

function moveLayerTo(level, state, layer, target) {
  let next = state;
  while (next.offsets[layer] !== target) {
    const delta = Math.sign(target - next.offsets[layer]);
    next = moveGameState(level, next, { layer, delta });
  }
  return next;
}

export function auditRewardEligibility(samples, baseSeed) {
  const perLayerReadAccuracy = 0.79;
  const detailChoiceRate = 0.55;
  let hintEligibleClosures = 0;
  let diagnosticEligibleClosures = 0;

  for (let index = 0; index < samples; index += 1) {
    const level = generateLevel(baseSeed + index);
    const rng = createRng(`novice:${baseSeed + index}`);
    let state = createGameState(level);
    for (let layer = 0; layer < level.layerCount; layer += 1) {
      const solution = level.layers[layer].solutionSlot;
      let perceived = solution;
      if (rng.next() >= perLayerReadAccuracy) perceived = solution + (rng.next() < 0.5 ? -1 : 1);
      state = moveLayerTo(level, state, layer, perceived);
    }

    let hintEligible = false;
    let diagnosticEligible = false;
    while (true) {
      state = attemptDrop(level, state);
      if (state.status === 'passed') break;
      const viewedDetails = rng.next() < detailChoiceRate;
      const eligibility = rewardEligibility(level, state, { viewDetails: viewedDetails });
      hintEligible ||= eligibility.hint;
      diagnosticEligible ||= eligibility.diagnostic;
      const blockerIndex = state.lastDrop.blockerIndex;
      state = retryLevel(state);
      state = moveLayerTo(level, state, blockerIndex, level.layers[blockerIndex].solutionSlot);
    }
    if (hintEligible) hintEligibleClosures += 1;
    if (diagnosticEligible) diagnosticEligibleClosures += 1;
  }

  return {
    samples,
    hintEligibleClosures,
    diagnosticEligibleClosures,
    hintRate: hintEligibleClosures / samples,
    diagnosticRate: diagnosticEligibleClosures / samples,
    proxyAssumptions: {
      perLayerReadAccuracy,
      detailChoiceRate,
      retryBehavior: 'correct the explicitly blocked layer, preserve all other placements',
    },
  };
}
