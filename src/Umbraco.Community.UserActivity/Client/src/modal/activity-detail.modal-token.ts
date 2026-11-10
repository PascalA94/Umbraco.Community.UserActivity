import { UmbModalToken } from '@umbraco-cms/backoffice/modal';

export interface ActivityDetailModalData {
  source: string;
  id: string;
  /** Source alias to localisation key, so the sidebar names the source as the table does. */
  sourceKeys: Record<string, string>;
}

export const ACTIVITY_DETAIL_MODAL_ALIAS = 'UserActivity.Modal.Detail';

export const ACTIVITY_DETAIL_MODAL = new UmbModalToken<ActivityDetailModalData, never>(ACTIVITY_DETAIL_MODAL_ALIAS, {
  modal: { type: 'sidebar', size: 'medium' },
});
