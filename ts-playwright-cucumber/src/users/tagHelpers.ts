import type { Pickle } from '@cucumber/messages';
import { userByUsername } from './userCatalog.ts';
import type { UserAccount } from './userAccount.ts';

const USER_TAG_PREFIX = '@user:';

/**
 * Returns the account named by a `@user:<username>` tag on the scenario, if present. Lets a scenario
 * bypass the per-worker assigned user and target one specific (often deliberately broken) saucedemo
 * account directly, e.g. `@user:locked_out_user`.
 */
export function getTaggedUser(pickle: Pickle): UserAccount | undefined {
  const tag = pickle.tags.find((t) => t.name.startsWith(USER_TAG_PREFIX));
  return tag ? userByUsername(tag.name.slice(USER_TAG_PREFIX.length)) : undefined;
}
