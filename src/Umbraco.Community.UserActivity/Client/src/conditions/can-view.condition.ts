import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import type { UmbConditionConfigBase, UmbConditionControllerArguments } from '@umbraco-cms/backoffice/extension-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UMB_USER_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/user';
import { UMB_CURRENT_USER_CONTEXT } from '@umbraco-cms/backoffice/current-user';
import { observeMultiple } from '@umbraco-cms/backoffice/observable-api';
import { userActivityApi } from '../api/user-activity.api.js';
import { createAccessCache, tabVisible } from './can-view.js';

// One answer per page session, shared by every tab the condition guards.
let cache: ReturnType<typeof createAccessCache> | undefined;
let cacheHost: UmbControllerHost | undefined;

function access(host: UmbControllerHost) {
  cacheHost = host;
  cache ??= createAccessCache(() => userActivityApi.filterOptions(cacheHost!));
  return cache.get();
}

/** Shows the Activity tab only to people the API would let read it. It hides UI; the API decides (design 7.2). */
export class UserActivityCanViewCondition extends UmbConditionBase<UmbConditionConfigBase> {
  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<UmbConditionConfigBase>) {
    super(host, args);
    this.permitted = false;

    this.consumeContext(UMB_CURRENT_USER_CONTEXT, (current) => {
      this.consumeContext(UMB_USER_WORKSPACE_CONTEXT, (workspace) => {
        if (!current || !workspace) {
          this.permitted = false;
          return;
        }
        this.observe(
          observeMultiple([current.unique, workspace.unique]),
          async ([currentUnique, unique]) => {
            const result = await access(this);
            this.permitted = tabVisible(result, unique ?? undefined, currentUnique ?? undefined);
          },
          '_userActivityCanView',
        );
      });
    });
  }
}

export { UserActivityCanViewCondition as api };
