import { generateAcceptedLevel } from './generator.js';
import {
  createInitialState,
  evaluateAttemptAnalytic,
  simulateAttempt,
  snapshotState,
} from './rules.js';
                                                                                             

export const DEFAULT_SOLVER_OPTIONS                = Object.freeze({
  columns: 24,
  rows: 40,
  timeStart: 0.35,
  timeEnd: 1.35,
  timeStep: 0.05,
  minX: -145,
  maxX: 145,
  minY: -145,
  maxY: 145,
});

function sampleCoordinate(minimum        , maximum        , index        , count        )         {
  return count <= 1 ? (minimum + maximum) / 2 : minimum + ((maximum - minimum) * index) / (count - 1);
}

export function sampleSolutions(
  level                 ,
  options                = DEFAULT_SOLVER_OPTIONS,
)                 {
  const timeSlices = Math.floor((options.timeEnd - options.timeStart) / options.timeStep + 1e-9) + 1;
  const sliceSize = options.columns * options.rows;
  const totalSamples = sliceSize * timeSlices;
  const solved = new Uint8Array(totalSamples);
  const heatmap = new Array        (sliceSize).fill(0);
  let successCount = 0;
  let firstSolution                    = null;
  let hasSpatialNeighbor = false;
  let hasTimeNeighbor = false;

  for (let timeIndex = 0; timeIndex < timeSlices; timeIndex += 1) {
    const time = options.timeStart + timeIndex * options.timeStep;
    for (let row = 0; row < options.rows; row += 1) {
      const y = sampleCoordinate(options.minY, options.maxY, row, options.rows);
      for (let column = 0; column < options.columns; column += 1) {
        const x = sampleCoordinate(options.minX, options.maxX, column, options.columns);
        const flat = timeIndex * sliceSize + row * options.columns + column;
        const result = evaluateAttemptAnalytic(level, { x, y, time });
        if (result.outcome !== 'success') continue;
        solved[flat] = 1;
        heatmap[row * options.columns + column] += 1;
        successCount += 1;
        firstSolution ??= { x, y, time };
        if (
          (column > 0 && solved[flat - 1] === 1) ||
          (row > 0 && solved[flat - options.columns] === 1)
        ) hasSpatialNeighbor = true;
        if (timeIndex > 0 && solved[flat - sliceSize] === 1) hasTimeNeighbor = true;
      }
    }
  }

  const visited = new Uint8Array(totalSamples);
  let largestComponent = 0;
  const queue = new Int32Array(totalSamples);
  const neighbors = [1, -1, options.columns, -options.columns, sliceSize, -sliceSize];
  for (let start = 0; start < totalSamples; start += 1) {
    if (solved[start] === 0 || visited[start] === 1) continue;
    let head = 0;
    let tail = 0;
    let componentSize = 0;
    queue[tail++] = start;
    visited[start] = 1;
    while (head < tail) {
      const current = queue[head++];
      componentSize += 1;
      const withinSlice = current % sliceSize;
      const column = withinSlice % options.columns;
      const row = Math.floor(withinSlice / options.columns);
      const timeIndex = Math.floor(current / sliceSize);
      for (let direction = 0; direction < neighbors.length; direction += 1) {
        if (direction === 0 && column === options.columns - 1) continue;
        if (direction === 1 && column === 0) continue;
        if (direction === 2 && row === options.rows - 1) continue;
        if (direction === 3 && row === 0) continue;
        if (direction === 4 && timeIndex === timeSlices - 1) continue;
        if (direction === 5 && timeIndex === 0) continue;
        const next = current + neighbors[direction];
        if (solved[next] === 0 || visited[next] === 1) continue;
        visited[next] = 1;
        queue[tail++] = next;
      }
    }
    largestComponent = Math.max(largestComponent, componentSize);
  }

  return {
    options,
    timeSlices,
    totalSamples,
    successCount,
    successRate: successCount / totalSamples,
    largestComponent,
    hasSpatialNeighbor,
    hasTimeNeighbor,
    heatmap,
    firstSolution,
  };
}

                                    
                    
                   
                   
                        
                  
                              
                        
                         
                          
   

export function auditSeeds(count        )              {
  let accepted = 0;
  let unsolved = 0;
  let nonContinuous = 0;
  let tooEasy = 0;
  let determinismFailures = 0;
  let resetFailures = 0;
  let maxSuccessRate = 0;
  let successRateTotal = 0;

  for (let index = 0; index < count; index += 1) {
    const seed = 10_000 + index * 7919;
    const level = generateAcceptedLevel(seed);
    const sample = sampleSolutions(level);
    const referenceFirst = simulateAttempt(level, level.referenceInput);
    const referenceSecond = simulateAttempt(level, level.referenceInput);
    const resetFirst = snapshotState(createInitialState(level));
    const resetSecond = snapshotState(createInitialState(level));
    if (referenceFirst.outcome !== 'success' || sample.successCount === 0) unsolved += 1;
    if (sample.largestComponent < 2) nonContinuous += 1;
    if (sample.successRate > 0.15) tooEasy += 1;
    if (JSON.stringify(referenceFirst) !== JSON.stringify(referenceSecond)) determinismFailures += 1;
    if (JSON.stringify(resetFirst) !== JSON.stringify(resetSecond)) resetFailures += 1;
    if (
      referenceFirst.outcome === 'success' &&
      sample.successCount > 0 &&
      sample.largestComponent >= 2 &&
      sample.successRate <= 0.15
    ) accepted += 1;
    maxSuccessRate = Math.max(maxSuccessRate, sample.successRate);
    successRateTotal += sample.successRate;
  }

  return {
    requested: count,
    accepted,
    unsolved,
    nonContinuous,
    tooEasy,
    determinismFailures,
    resetFailures,
    maxSuccessRate,
    meanSuccessRate: count === 0 ? 0 : successRateTotal / count,
  };
}
