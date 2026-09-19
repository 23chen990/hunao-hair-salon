import { computeLayout } from '../src/layout.ts';

const ARENA_RADIUS = 150;
const ARENA_PADDING = 12;

export function modelToCanvas(point, width, height) {
  const layout = computeLayout(width, height);
  const scale = (layout.arena.size / 2 - ARENA_PADDING) / ARENA_RADIUS;
  return {
    x: (layout.arena.left + layout.arena.right) / 2 + point.x * scale,
    y: (layout.arena.top + layout.arena.bottom) / 2 + point.y * scale,
  };
}

export function buildCapturePlan(scenarios, acceptanceSeeds) {
  const seedIndexes = new Map(acceptanceSeeds.map((seed, index) => [seed, index]));
  const plan = [...scenarios].sort((left, right) => left.runOrder - right.runOrder);
  let priorSeedIndex = -1;
  for (const scenario of plan) {
    const seedIndex = seedIndexes.get(scenario.seed);
    if (seedIndex === undefined) throw new Error(`Unknown acceptance seed: ${scenario.seed}`);
    if (seedIndex < priorSeedIndex) throw new Error('Capture plan cannot move backwards through seeds');
    priorSeedIndex = seedIndex;
  }
  return plan;
}
