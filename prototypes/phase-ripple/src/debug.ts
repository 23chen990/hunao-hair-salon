import { generateAcceptedLevel } from './generator.ts';
import { ACCEPTANCE_SEEDS } from './levels.ts';
import { DEFAULT_SOLVER_OPTIONS, sampleSolutions } from './solver.ts';

const canvas = document.querySelector<HTMLCanvasElement>('#heatmap');
const select = document.querySelector<HTMLSelectElement>('#seed-select');
const metrics = document.querySelector<HTMLElement>('#metrics');
if (canvas === null || select === null || metrics === null) throw new Error('Debug page is incomplete');
const context = canvas.getContext('2d');
if (context === null) throw new Error('Canvas 2D is unavailable');

for (const seed of ACCEPTANCE_SEEDS) {
  const option = document.createElement('option');
  option.value = String(seed);
  option.textContent = `SEED ${seed}`;
  select.append(option);
}

function gridPosition(value: number, minimum: number, maximum: number, size: number): number {
  return ((value - minimum) / (maximum - minimum)) * size;
}

function draw(): void {
  const seed = Number(select.value || ACCEPTANCE_SEEDS[0]);
  const level = generateAcceptedLevel(seed);
  const sample = sampleSolutions(level);
  const width = canvas.width;
  const height = canvas.height;
  const cellWidth = width / sample.options.columns;
  const cellHeight = height / sample.options.rows;
  const strongest = Math.max(1, ...sample.heatmap);

  context.fillStyle = '#071013';
  context.fillRect(0, 0, width, height);
  for (let row = 0; row < sample.options.rows; row += 1) {
    for (let column = 0; column < sample.options.columns; column += 1) {
      const count = sample.heatmap[row * sample.options.columns + column] ?? 0;
      const strength = count / strongest;
      context.fillStyle = count === 0
        ? 'rgba(25,48,54,0.34)'
        : `rgba(108,245,221,${0.16 + strength * 0.84})`;
      context.fillRect(
        column * cellWidth + 0.5,
        row * cellHeight + 0.5,
        Math.max(1, cellWidth - 1),
        Math.max(1, cellHeight - 1),
      );
    }
  }

  const referenceX = gridPosition(
    level.referenceInput.x,
    sample.options.minX,
    sample.options.maxX,
    width,
  );
  const referenceY = gridPosition(
    level.referenceInput.y,
    sample.options.minY,
    sample.options.maxY,
    height,
  );
  context.beginPath();
  context.arc(referenceX, referenceY, 7, 0, Math.PI * 2);
  context.strokeStyle = '#ff4d58';
  context.lineWidth = 2;
  context.stroke();

  metrics.textContent = [
    `pieces=${level.pieces.length}`,
    `generationAttempt=${level.generationAttempt}`,
    `grid=${sample.options.columns}x${sample.options.rows}x${sample.timeSlices}`,
    `time=${DEFAULT_SOLVER_OPTIONS.timeStart.toFixed(2)}..${DEFAULT_SOLVER_OPTIONS.timeEnd.toFixed(2)}s / ${DEFAULT_SOLVER_OPTIONS.timeStep.toFixed(2)}s`,
    `solutions=${sample.successCount}/${sample.totalSamples}`,
    `randomSuccess=${(sample.successRate * 100).toFixed(2)}%`,
    `largestComponent=${sample.largestComponent}`,
    `spatialNeighbor=${sample.hasSpatialNeighbor}`,
    `timeNeighbor=${sample.hasTimeNeighbor}`,
    `reference=(${level.referenceInput.x.toFixed(1)}, ${level.referenceInput.y.toFixed(1)}, ${level.referenceInput.time.toFixed(2)}s)`,
  ].join('\n');
}

select.addEventListener('change', draw);
select.value = String(ACCEPTANCE_SEEDS[0]);
draw();
