// @vitest-environment happy-dom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// The backoffice isn't loaded in tests: each module the profile route touches is replaced by the smallest stand-in
// that keeps its contract (profile-route review PR-1).
const api = vi.hoisted(() => ({
  filterOptions: vi.fn(),
  list: vi.fn(),
  listForUser: vi.fn(),
  exportCsv: vi.fn(),
  detail: vi.fn(),
}));
const openModal = vi.hoisted(() => vi.fn());
const accessMock = vi.hoisted(() => vi.fn());
const SIGNED_IN_USER = '00000000-0000-4000-8000-0000000000aa'; // gitleaks:allow (a fake user key)

// The view and the profile box take Lit's decorators from the backoffice's re-export, which the test alias maps to
// `lit`; add them there.
vi.mock('lit', async (importOriginal) => ({ ...(await importOriginal<object>()), ...(await import('lit/decorators.js')) }));
vi.mock('@umbraco-cms/backoffice/lit-element', async () => {
  const { LitElement } = await import('lit');
  class UmbLitElement extends LitElement {
    localize = { term: (key: string) => key };
    // The current-user context is the only one consumed here; it hands back an object whose `unique` is the key.
    consumeContext(_token: unknown, callback: (context: unknown) => void) {
      callback({ unique: SIGNED_IN_USER });
    }
    observe<T>(source: T, callback: (value: T) => void) {
      callback(source);
    }
  }
  return { UmbLitElement };
});
vi.mock('@umbraco-cms/backoffice/modal', () => ({
  umbOpenModal: openModal,
  UmbModalToken: class {
    constructor(public alias: string) {}
  },
}));
vi.mock('@umbraco-cms/backoffice/current-user', () => ({ UMB_CURRENT_USER_CONTEXT: 'current-user' }));
vi.mock('../src/api/user-activity.api.js', () => ({ userActivityApi: api }));
vi.mock('../src/conditions/can-view.condition.js', () => ({ access: accessMock }));

const { UserActivityViewElement } = await import('../src/views/shared/activity-view.element.js');
const { UserActivityMyActivityProfileAppElement } = await import('../src/profile/my-activity-profile-app.element.js');
const { MY_ACTIVITY_MODAL_ALIAS } = await import('../src/modal/my-activity.modal-token.js');

const page = { items: [], total: 120, totalIsExact: true, depthCapped: false, sources: [] };
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

beforeEach(() => {
  api.filterOptions.mockResolvedValue({ data: { canReadAll: false, defaultRangeDays: 30, sources: [], categories: [], entityTypes: [] } });
  api.listForUser.mockResolvedValue({ data: page });
  api.list.mockResolvedValue({ data: page });
  window.history.replaceState(null, '', '/umbraco/section/content?search=from-the-page-underneath');
});

afterEach(() => {
  document.body.innerHTML = '';
  vi.restoreAllMocks();
  api.listForUser.mockReset();
  api.list.mockReset();
  openModal.mockReset();
  accessMock.mockReset();
});

async function mountView(noUrl: boolean) {
  const view = new UserActivityViewElement();
  view.userKey = SIGNED_IN_USER;
  view.noUrl = noUrl;
  document.body.append(view);
  await settle();
  await view.updateComplete;
  return view;
}

const pageChange = (view: HTMLElement, page: number) =>
  view.shadowRoot!.querySelector('activity-pager')!.dispatchEvent(new CustomEvent('page-change', { detail: { page }, bubbles: true, composed: true }));

describe('"Your activity" view (no-url mode)', () => {
  it('NoUrl_IgnoresTheQueryStringOfThePageUnderneath', async () => {
    await mountView(true);
    const [, , query] = api.listForUser.mock.calls[0];
    expect(api.listForUser.mock.calls[0][1]).toBe(SIGNED_IN_USER);
    expect(query.search).toBeUndefined();
  });

  it('NoUrl_PagingAndFilteringNeverWriteHistory', async () => {
    const push = vi.spyOn(window.history, 'pushState');
    const replace = vi.spyOn(window.history, 'replaceState');
    const view = await mountView(true);

    pageChange(view, 2);
    view.shadowRoot!.querySelector('activity-filter-bar')!.dispatchEvent(
      new CustomEvent('filters-change', { detail: { sources: ['core'], categories: [], entityTypes: [], search: '', page: 1 }, bubbles: true, composed: true }),
    );
    await settle();

    expect(push).not.toHaveBeenCalled();
    expect(replace).not.toHaveBeenCalled();
    expect(window.location.search).toBe('?search=from-the-page-underneath');
    expect(api.listForUser).toHaveBeenCalledTimes(3);
  });

  it('NoUrl_BackButtonDoesNotReloadTheView', async () => {
    await mountView(true);
    const calls = api.listForUser.mock.calls.length;
    window.dispatchEvent(new PopStateEvent('popstate'));
    await settle();
    expect(api.listForUser.mock.calls.length).toBe(calls);
  });

  it('WithUrl_PagingStillPushesHistory (the tab and the global view are unchanged)', async () => {
    const push = vi.spyOn(window.history, 'pushState');
    const view = await mountView(false);
    pageChange(view, 2);
    await settle();
    expect(push).toHaveBeenCalledTimes(1);
  });
});

describe('"Your activity" profile box', () => {
  async function mountBox(kind: 'reader' | 'self' | 'none') {
    accessMock.mockResolvedValue({ kind });
    const box = new UserActivityMyActivityProfileAppElement();
    document.body.append(box);
    await settle();
    await box.updateComplete;
    return box;
  }

  it('NoAccess_RendersNothingAndHidesTheHost', async () => {
    const box = await mountBox('none');
    expect(box.hidden).toBe(true);
    expect(box.shadowRoot!.querySelector('uui-box')).toBeNull();
  });

  it.each(['reader', 'self'] as const)('%s_OpensYourActivityForTheSignedInUserOnly', async (kind) => {
    const box = await mountBox(kind);
    expect(box.hidden).toBe(false);
    openModal.mockRejectedValue(new Error('closed'));
    box.shadowRoot!.querySelector('uui-button')!.dispatchEvent(new Event('click'));
    await settle();
    expect(openModal).toHaveBeenCalledTimes(1);
    const [, token, args] = openModal.mock.calls[0];
    expect(token.alias).toBe(MY_ACTIVITY_MODAL_ALIAS);
    expect(args.data.userKey).toBe(SIGNED_IN_USER);
  });
});
