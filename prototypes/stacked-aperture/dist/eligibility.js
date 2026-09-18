export function createEligibilitySession({ placeholdersEnabled = false } = {}) {
  return {
    placeholdersEnabled,
    wallClockMs: 0,
    gameClockMs: 0,
    roundNumber: 0,
    page: 'play',
    lastFullscreenEligibilityMs: null,
    lastRewardResumeMs: null,
    interstitialEligibilityCount: 0,
    rewardEligibilityCount: 0,
    adSimulation: null,
  };
}

export function advanceClock(session, durationMs) {
  if (!Number.isFinite(durationMs) || durationMs < 0) throw new RangeError('duration must be non-negative');
  return {
    ...session,
    wallClockMs: session.wallClockMs + durationMs,
    gameClockMs: session.gameClockMs + (session.adSimulation ? 0 : durationMs),
  };
}

export function completeRound(session, durationMs) {
  let next = advanceClock({ ...session, page: 'play' }, durationMs);
  next = { ...next, roundNumber: next.roundNumber + 1, page: 'settlement' };
  const sinceFullscreen = next.gameClockMs - (next.lastFullscreenEligibilityMs ?? 0);
  const sinceReward = next.lastRewardResumeMs === null
    ? Number.POSITIVE_INFINITY
    : next.gameClockMs - next.lastRewardResumeMs;
  const eligible = next.placeholdersEnabled
    && next.roundNumber >= 4
    && sinceFullscreen >= 180_000
    && sinceReward >= 60_000
    && next.interstitialEligibilityCount < 2;
  if (!eligible) return { session: next, events: [] };
  const event = {
    type: 'interstitial_eligible',
    page: 'settlement',
    roundNumber: next.roundNumber,
    gameClockMs: next.gameClockMs,
  };
  next = {
    ...next,
    lastFullscreenEligibilityMs: next.gameClockMs,
    interstitialEligibilityCount: next.interstitialEligibilityCount + 1,
  };
  return { session: next, events: [event] };
}

export function rewardEligibility(level, gameState, { viewDetails = false } = {}) {
  const blockerIndex = gameState.lastDrop?.blockerIndex;
  return {
    hint: gameState.status === 'blocked' && gameState.consecutiveFailures >= 2,
    diagnostic: gameState.status === 'blocked'
      && viewDetails
      && Number.isInteger(blockerIndex)
      && blockerIndex >= Math.ceil(level.layerCount / 2),
  };
}

export function registerRewardEntry(session, type) {
  if (type !== 'hint' && type !== 'diagnostic') throw new TypeError('unsupported reward entry type');
  if (!session.placeholdersEnabled || session.rewardEligibilityCount >= 2) {
    return { session, events: [] };
  }
  const next = { ...session, rewardEligibilityCount: session.rewardEligibilityCount + 1 };
  return {
    session: next,
    events: [{
      type: `${type}_eligible`,
      page: session.page,
      gameClockMs: session.gameClockMs,
    }],
  };
}

export function beginAdSimulation(session, type) {
  if (!session.placeholdersEnabled) throw new Error('ad placeholders are disabled');
  if (session.adSimulation) throw new Error('an ad simulation is already active');
  if (type !== 'rewarded' && type !== 'interstitial') throw new TypeError('unsupported ad simulation type');
  return { ...session, adSimulation: { type, startedAtWallClockMs: session.wallClockMs } };
}

export function endAdSimulation(session) {
  if (!session.adSimulation) throw new Error('no ad simulation is active');
  return {
    ...session,
    lastRewardResumeMs: session.adSimulation.type === 'rewarded'
      ? session.gameClockMs
      : session.lastRewardResumeMs,
    adSimulation: null,
  };
}
