import { DEFAULT_RULES } from './rules.js';
                                                  

export const ACCEPTANCE_SEEDS = Object.freeze([
  17, 29, 43, 61, 79, 97, 113, 131, 149, 167,
  191, 223, 257, 293, 331, 373, 419, 463, 509, 557,
]);

function tutorialLevel(
  seed        ,
  referenceInput                                                  ,
  targetArrival        ,
  hits                                                        ,
)                  {
  return {
    seed,
    generationAttempt: 0,
    referenceInput,
    targetArrival,
    pieces: hits.map((hit, id) => {
      const x = Math.cos(hit.angle) * hit.radius;
      const y = Math.sin(hit.angle) * hit.radius;
      const distance = Math.hypot(x - referenceInput.x, y - referenceInput.y);
      const triggerTime = referenceInput.time + distance / DEFAULT_RULES.rippleSpeed;
      const speed = hit.radius / (targetArrival - triggerTime);
      const startRadius = speed * (targetArrival - 2 * triggerTime);
      return { id, angle: hit.angle, startRadius, speed };
    }),
  };
}

export const TUTORIAL_LEVELS = Object.freeze([
  tutorialLevel(-101, { x: 0, y: 0, time: 0.4 }, 2.6, [
    { angle: -Math.PI / 2, radius: 80 },
  ]),
  tutorialLevel(-102, { x: 48, y: -10, time: 0.5 }, 3.2, [
    { angle: Math.PI, radius: 92 },
    { angle: 0, radius: 110 },
  ]),
]);
