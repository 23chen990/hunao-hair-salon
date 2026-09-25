export type RandomSource = Readonly<{
  next: () => number;
  range: (minimum: number, maximum: number) => number;
  integer: (minimum: number, maximumInclusive: number) => number;
}>;

function normalizeSeed(seed: number): number {
  return (Math.trunc(seed) >>> 0) || 0x6d2b79f5;
}

export function createRandom(seed: number): RandomSource {
  let state = normalizeSeed(seed);
  const next = (): number => {
    state = (state + 0x6d2b79f5) >>> 0;
    let value = state;
    value = Math.imul(value ^ (value >>> 15), value | 1);
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61);
    return ((value ^ (value >>> 14)) >>> 0) / 4294967296;
  };

  return {
    next,
    range: (minimum, maximum) => minimum + (maximum - minimum) * next(),
    integer: (minimum, maximumInclusive) =>
      Math.floor(minimum + next() * (maximumInclusive - minimum + 1)),
  };
}

export function mixSeed(seed: number, attempt: number): number {
  let value = (Math.trunc(seed) ^ Math.imul(attempt + 1, 0x9e3779b1)) >>> 0;
  value ^= value >>> 16;
  value = Math.imul(value, 0x7feb352d);
  value ^= value >>> 15;
  value = Math.imul(value, 0x846ca68b);
  value ^= value >>> 16;
  return value >>> 0;
}
