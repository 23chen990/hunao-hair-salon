function normalizeSeed(seed        )         {
  return (Math.trunc(seed) >>> 0) || 0x6d2b79f5;
}

function createRandom(seed        )               {
  let state = normalizeSeed(seed);
  const next = ()         => {
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

function mixSeed(seed        , attempt        )         {
  let value = (Math.trunc(seed) ^ Math.imul(attempt + 1, 0x9e3779b1)) >>> 0;
  value ^= value >>> 16;
  value = Math.imul(value, 0x7feb352d);
  value ^= value >>> 15;
  value = Math.imul(value, 0x846ca68b);
  value ^= value >>> 16;
  return value >>> 0;
}

module.exports.createRandom = createRandom;
module.exports.mixSeed = mixSeed;
