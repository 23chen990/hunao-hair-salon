export type Point = Readonly<{ x: number; y: number }>;

export type ClickInput = Readonly<Point & { time: number }>;

export type AttemptOutcome = 'playing' | 'success' | 'timeout' | 'boundary';

export type PieceDefinition = Readonly<{
  id: number;
  angle: number;
  startRadius: number;
  speed: number;
}>;

export type LevelDefinition = Readonly<{
  seed: number;
  generationAttempt: number;
  referenceInput: ClickInput;
  targetArrival: number;
  pieces: readonly PieceDefinition[];
}>;

export type PieceState = Readonly<{
  id: number;
  angle: number;
  radius: number;
  speed: number;
  reversed: boolean;
  triggerTime: number | null;
  arrivalTime: number | null;
}>;

export type GameState = Readonly<{
  level: LevelDefinition;
  time: number;
  click: ClickInput | null;
  pieces: readonly PieceState[];
  triggerOrder: readonly number[];
  firstArrival: number | null;
  outcome: AttemptOutcome;
  resultTime: number | null;
}>;

export type AttemptResult = Readonly<{
  outcome: Exclude<AttemptOutcome, 'playing'>;
  triggerOrder: readonly number[];
  triggerTimes: readonly number[];
  arrivalTimes: readonly number[];
  arrivalSpread: number;
  resultTime: number;
}>;

export type SolverOptions = Readonly<{
  columns: number;
  rows: number;
  timeStart: number;
  timeEnd: number;
  timeStep: number;
  minX: number;
  maxX: number;
  minY: number;
  maxY: number;
}>;

export type SolutionSample = Readonly<{
  options: SolverOptions;
  timeSlices: number;
  totalSamples: number;
  successCount: number;
  successRate: number;
  largestComponent: number;
  hasSpatialNeighbor: boolean;
  hasTimeNeighbor: boolean;
  heatmap: readonly number[];
  firstSolution: ClickInput | null;
}>;
