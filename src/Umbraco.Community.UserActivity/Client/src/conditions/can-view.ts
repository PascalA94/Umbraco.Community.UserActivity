import type { ActivityFilterOptionsResponseModel } from '../api/user-activity.api.js';

export type CanViewAccess = { kind: 'reader' } | { kind: 'self' } | { kind: 'none' };

interface OptionsResult {
  data?: ActivityFilterOptionsResponseModel;
  error?: unknown;
}

const statusOf = (error: unknown) => (error as { status?: number } | undefined)?.status;

/** filter-options answers 200 to readers and self-view users and 403 to everyone else (design 5). */
export function accessFromResult(result: OptionsResult): CanViewAccess {
  if (result.data) return result.data.canReadAll ? { kind: 'reader' } : { kind: 'self' };
  return { kind: 'none' };
}

/**
 * Whether the tab shows on this user's workspace. Readers see every tab, their own included; a self-view user
 * sees only their own. This only hides UI: the API decides (design 7.2).
 */
export function tabVisible(access: CanViewAccess, workspaceUserKey: string | undefined, currentUserKey: string | undefined): boolean {
  if (access.kind === 'reader') return !!workspaceUserKey;
  if (access.kind === 'self') return !!workspaceUserKey && workspaceUserKey === currentUserKey;
  return false;
}

/** The profile's "Your activity" box: for anyone the API would show their own activity (readers and self-view users). */
export function profileVisible(access: CanViewAccess): boolean {
  return access.kind !== 'none';
}

/**
 * Asks filter-options once per page session and shares the answer with every tab. A definite answer (200 or 403)
 * is kept; anything else, such as a network error while signing in, is not, so the next tab asks again.
 */
export function createAccessCache<THost = unknown>(fetcher: (host?: THost) => Promise<OptionsResult>) {
  let pending: Promise<CanViewAccess> | undefined;
  return {
    get(host?: THost): Promise<CanViewAccess> {
      pending ??= fetcher(host).then(
        (result) => {
          const definite = !!result.data || statusOf(result.error) === 403;
          if (!definite) pending = undefined;
          return accessFromResult(result);
        },
        () => {
          pending = undefined;
          return { kind: 'none' } as CanViewAccess;
        },
      );
      return pending;
    },
  };
}
