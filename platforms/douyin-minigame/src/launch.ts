import { isSidebarLaunchOptions } from './platform.ts';

declare const tt: Readonly<Record<string, any>> | undefined;

let latestLaunchOptions: Readonly<Record<string, unknown>> = {};

function remember(options: Readonly<Record<string, unknown>> | null | undefined): void {
  latestLaunchOptions = options ?? {};
}

if (typeof tt !== 'undefined') {
  if (typeof tt.getLaunchOptionsSync === 'function') {
    try {
      remember(tt.getLaunchOptionsSync());
    } catch {
      // onShow remains the authoritative path.
    }
  }
  if (typeof tt.onShow === 'function') tt.onShow(remember);
}

export function launchedFromSidebar(): boolean {
  return isSidebarLaunchOptions(latestLaunchOptions);
}
