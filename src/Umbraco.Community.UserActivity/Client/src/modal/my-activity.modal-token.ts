import { UmbModalToken } from '@umbraco-cms/backoffice/modal';

export interface MyActivityModalData {
  /** The signed-in user's key: the view is locked to it, and the API checks it again. */
  userKey: string;
}

import { MY_ACTIVITY_MODAL_ALIAS } from './aliases.js';

export { MY_ACTIVITY_MODAL_ALIAS };

export const MY_ACTIVITY_MODAL = new UmbModalToken<MyActivityModalData, never>(MY_ACTIVITY_MODAL_ALIAS, {
  modal: { type: 'sidebar', size: 'full' },
});
