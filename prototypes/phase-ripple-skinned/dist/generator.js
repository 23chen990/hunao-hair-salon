import { createRandom, mixSeed } from './prng.js';
import { DEFAULT_RULES } from './rules.js';
                                                                   

function generateCandidate(seed        , generationAttempt        )                  {
  const random = createRandom(mixSeed(seed, generationAttempt));
  const pieceCount = 3 + ((Math.abs(Math.trunc(seed)) + generationAttempt) % 4);
  const clickAngle = random.range(-Math.PI, Math.PI);
  const clickRadius = random.range(48, 82);
  const referenceInput = {
    x: Math.cos(clickAngle) * clickRadius,
    y: Math.sin(clickAngle) * clickRadius,
    time: random.range(0.5, 0.88),
  };
  const targetArrival = random.range(3.85, 4.45);
  const phase = random.range(-Math.PI, Math.PI);
  const pieces                    = [];

  for (let index = 0; index < pieceCount; index += 1) {
    const evenAngle = phase + (index / pieceCount) * Math.PI * 2;
    const angle = evenAngle + random.range(-0.22, 0.22);
    const triggerRadius = random.range(88, 132);
    const hitX = Math.cos(angle) * triggerRadius;
    const hitY = Math.sin(angle) * triggerRadius;
    const distanceFromClick = Math.hypot(hitX - referenceInput.x, hitY - referenceInput.y);
    const triggerTime = referenceInput.time + distanceFromClick / DEFAULT_RULES.rippleSpeed;
    const speed = triggerRadius / (targetArrival - triggerTime);
    const startRadius = speed * (targetArrival - 2 * triggerTime);
    pieces.push({ id: index, angle, startRadius, speed });
  }

  return { seed, generationAttempt, referenceInput, targetArrival, pieces };
}

export function generateLevel(seed        , generationAttempt = 0)                  {
  return generateCandidate(seed, generationAttempt);
}

const acceptedCache = new Map                         ();

export function generateAcceptedLevel(seed        )                  {
  const cached = acceptedCache.get(seed);
  if (cached !== undefined) return cached;

  for (let attempt = 0; attempt < 96; attempt += 1) {
    const candidate = generateCandidate(seed, attempt);
    const validGeometry = candidate.pieces.every((piece) =>
      piece.startRadius >= 10 &&
      piece.startRadius < DEFAULT_RULES.arenaRadius - DEFAULT_RULES.pieceRadius - 8 &&
      piece.speed >= 18 &&
      piece.speed <= 62,
    );
    if (!validGeometry) continue;

    const result = evaluateForAcceptance(candidate);
    if (result.accepted) {
      acceptedCache.set(seed, candidate);
      return candidate;
    }
  }
  throw new Error(`No accepted phase-ripple level found for seed ${seed}`);
}

function evaluateForAcceptance(level                 )                        {
  // Dynamic import would make browser generation asynchronous. This compact probe mirrors the
  // public solver's grid and is kept here so accepted levels remain synchronous and deterministic.
  const columns = 16;
  const rows = 24;
  const times = 13;
  let successes = 0;
  let adjacent = false;
  const previous = new Uint8Array(columns * rows);
  const current = new Uint8Array(columns * rows);

  for (let timeIndex = 0; timeIndex < times; timeIndex += 1) {
    current.fill(0);
    const time = 0.35 + timeIndex * 0.075;
    for (let row = 0; row < rows; row += 1) {
      const y = -145 + (290 * row) / (rows - 1);
      for (let column = 0; column < columns; column += 1) {
        const x = -145 + (290 * column) / (columns - 1);
        const index = row * columns + column;
        if (analyticSuccess(level, x, y, time)) {
          current[index] = 1;
          successes += 1;
          if (
            (column > 0 && current[index - 1] === 1) ||
            (row > 0 && current[index - columns] === 1) ||
            previous[index] === 1
          ) adjacent = true;
        }
      }
    }
    previous.set(current);
  }
  const total = columns * rows * times;
  return { accepted: successes > 0 && adjacent && successes / total <= 0.13 };
}

function analyticSuccess(level                 , x        , y        , clickTime        )          {
  const limit = DEFAULT_RULES.arenaRadius - DEFAULT_RULES.pieceRadius;
  const arrivals           = [];
  for (const piece of level.pieces) {
    const boundaryTime = (limit - piece.startRadius) / piece.speed;
    if (clickTime >= boundaryTime) return false;
    const ux = Math.cos(piece.angle);
    const uy = Math.sin(piece.angle);
    const radius = piece.startRadius + piece.speed * clickTime;
    const dx = ux * radius - x;
    const dy = uy * radius - y;
    const a = piece.speed ** 2 - DEFAULT_RULES.rippleSpeed ** 2;
    const b = 2 * piece.speed * (dx * ux + dy * uy);
    const c = dx * dx + dy * dy;
    const tau = (-b - Math.sqrt(Math.max(0, b * b - 4 * a * c))) / (2 * a);
    const trigger = clickTime + tau;
    if (!Number.isFinite(trigger) || trigger >= boundaryTime) return false;
    arrivals.push(piece.startRadius / piece.speed + 2 * trigger);
  }
  return Math.max(...arrivals) - Math.min(...arrivals) <= DEFAULT_RULES.syncWindow;
}
