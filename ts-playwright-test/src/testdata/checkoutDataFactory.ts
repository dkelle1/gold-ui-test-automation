import { faker } from '@faker-js/faker';
import type { CheckoutInfo } from './checkoutInfo';

/**
 * Generates realistic checkout form data with Faker. Login credentials are never generated this way -
 * see `userCatalog.ts` - only data the app accepts freely.
 *
 * Uses faker-js's shared singleton, which is safe here for the same reason it is in the Cucumber
 * sibling, and if anything more clearly so: a Playwright worker is a separate OS *process* (not a
 * worker thread), with its own module registry and its own copy of this module, running one test at a
 * time. There is no shared-memory concurrency for a per-call `Faker<T>` instance to protect against,
 * which is why the two C# siblings need one and this does not.
 */
export function createCheckoutInfo(): CheckoutInfo {
  return {
    firstName: faker.person.firstName(),
    lastName: faker.person.lastName(),
    postalCode: faker.location.zipCode('#####')
  };
}
