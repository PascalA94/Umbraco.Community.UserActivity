import type { ActivityActorResponseModel, ActivityEntityResponseModel } from '../api/user-activity.api.js';
import type { Localize } from './localize.js';

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const BASE = '/umbraco/section';

/** A localised term, or the fallback when the key has no translation (third-party categories, aliases). */
export function term(localize: Localize, key: string, fallback: string): string {
  const value = localize(key);
  return value && value !== key ? value : fallback;
}

/** Name for an actor: the current name, then the stored name, then the server's English label. */
export function actorName(actor: ActivityActorResponseModel): string {
  return actor.name || actor.storedName || actor.label;
}

const KIND_KEYS: Record<string, string> = {
  system: 'userActivity_actorSystem',
  defaultAdmin: 'userActivity_actorDefaultAdmin',
  unknown: 'userActivity_actorUnknown',
  automation: 'userActivity_actorAutomation',
  serviceAccount: 'userActivity_actorServiceAccount',
};

/** Like {@link actorName}, but a nameless system, default-admin or unknown actor gets its translated label. */
export function actorDisplay(actor: ActivityActorResponseModel, localize: Localize): string {
  const named = actor.name || actor.storedName;
  if (named) return named;
  const key = Object.hasOwn(KIND_KEYS, actor.kind) ? KIND_KEYS[actor.kind] : undefined;
  return key ? term(localize, key, actor.label) : actor.label;
}

/** Link to a user's Activity tab. Built from constants and a validated GUID, never from event text. */
export function userTabHref(userKey: string | null | undefined): string | undefined {
  return userKey && GUID.test(userKey) ? `${BASE}/user-management/workspace/user/edit/${userKey}/view/activity` : undefined;
}

const ITEM_PATHS: Record<string, (key: string) => string> = {
  document: (key) => `${BASE}/content/workspace/document/edit/${key}`,
  media: (key) => `${BASE}/media/workspace/media/edit/${key}`,
};

/** Link to an item that still exists, for the types core opens by key. */
export function itemHref(entity: ActivityEntityResponseModel | null | undefined): string | undefined {
  if (!entity?.exists || !entity.key || !GUID.test(entity.key)) return undefined;
  const type = entity.type.toLowerCase();
  return Object.hasOwn(ITEM_PATHS, type) ? ITEM_PATHS[type]!(entity.key) : undefined;
}

export function relativeTime(iso: string, now: Date, locale?: string): string {
  const seconds = Math.round((new Date(iso).getTime() - now.getTime()) / 1000);
  const formatter = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' });
  const abs = Math.abs(seconds);
  if (abs < 60) return formatter.format(seconds, 'second');
  if (abs < 3600) return formatter.format(Math.round(seconds / 60), 'minute');
  if (abs < 86_400) return formatter.format(Math.round(seconds / 3600), 'hour');
  if (abs < 86_400 * 30) return formatter.format(Math.round(seconds / 86_400), 'day');
  if (abs < 86_400 * 365) return formatter.format(Math.round(seconds / (86_400 * 30)), 'month');
  return formatter.format(Math.round(seconds / (86_400 * 365)), 'year');
}

export function fullDate(iso: string, locale?: string): string {
  return new Date(iso).toLocaleString(locale, { dateStyle: 'full', timeStyle: 'long' });
}
