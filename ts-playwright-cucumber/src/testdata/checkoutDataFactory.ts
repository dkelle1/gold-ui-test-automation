import { faker } from '@faker-js/faker';
import type { CheckoutInfo } from './checkoutInfo.ts';

/**
 * Generates realistic checkout form data with Faker. Login credentials are never generated this way -
 * see `userCatalog.ts` - only data the app accepts freely.
 *
 * Unlike the C# siblings, which build a fresh `Faker<T>` per call specifically because a shared,
 * process-wide instance would be a thread-safety hazard under their multi-threaded parallelism, this
 * uses faker-js's shared `faker` singleton directly - the idiomatic way to use this library. That is
 * safe here: each Cucumber.js worker is its own Node `worker_threads` isolate with its own private copy
 * of every imported module (including this one), and a worker only ever runs one scenario at a time, so
 * there is no concurrent access to guard against the way there would be under shared-memory threading.
 */
export function createCheckoutInfo(): CheckoutInfo {
  return {
    firstName: faker.person.firstName(),
    lastName: faker.person.lastName(),
    postalCode: faker.location.zipCode('#####')
  };
}
