/** Presentational elements take a resolver instead of reaching for the backoffice, so tests can render them. */
export type Localize = (key: string, ...args: unknown[]) => string;
export const identityLocalize: Localize = (key) => key;

const OWN_PREFIX = 'userActivity_';

/**
 * The one place a localisation key may be dynamic (categories, entity types and provider source keys arrive as data).
 * Only this package's own keys are resolved; anything else returns the key unchanged, which callers treat as "no
 * translation" and replace with the alias. build/check-xss.sh exempts this file and nothing else.
 */
export function makeLocalize(// The controller's term() is typed per known key on each line; `any` keeps one helper compiling against both.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
  controller: { term: (key: any, ...args: any[]) => string }): Localize {
  return (key, ...args) => (key.startsWith(OWN_PREFIX) ? controller.term(key, ...args) : key);
}
