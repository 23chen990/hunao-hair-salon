export type StorageLike = Pick<Storage, 'getItem' | 'setItem'>;

export type EventType =
  | 'level_start'
  | 'first_click'
  | 'level_result'
  | 'retry'
  | 'next_level';

export type LocalEvent = Readonly<{
  type: EventType;
  at: number;
  data: Readonly<Record<string, unknown>>;
}>;

export class LocalEventLog {
  private readonly storage: StorageLike;
  private readonly key: string;
  private readonly now: () => number;

  constructor(
    storage: StorageLike,
    key = 'phase-ripple-events-v1',
    now: () => number = () => Date.now(),
  ) {
    this.storage = storage;
    this.key = key;
    this.now = now;
  }

  read(): LocalEvent[] {
    try {
      const stored = this.storage.getItem(this.key);
      if (stored === null) return [];
      const value = JSON.parse(stored);
      return Array.isArray(value) ? value : [];
    } catch {
      return [];
    }
  }

  record(type: EventType, data: Readonly<Record<string, unknown>>): LocalEvent {
    const event: LocalEvent = { type, at: this.now(), data };
    const events = [...this.read(), event].slice(-500);
    this.storage.setItem(this.key, JSON.stringify(events));
    return event;
  }
}

export function createMemoryStorage(): StorageLike {
  const values = new Map<string, string>();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => { values.set(key, value); },
  };
}
