import { attemptDrop, createGameState, generateLevel, moveGameState } from './model.js';
import { solveLevel } from './solver.js';

export function createEvidenceFrame(seed) {
  const level = generateLevel(seed);
  const solution = solveLevel(level);
  const appliedMoves = solution.moves.slice(0, -1);
  let state = createGameState(level);
  for (const move of appliedMoves) state = moveGameState(level, state, move);
  state = attemptDrop(level, state);
  return { level, solution, appliedMoves, state };
}
