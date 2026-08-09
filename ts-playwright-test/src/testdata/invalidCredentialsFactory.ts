import { faker } from '@faker-js/faker';
import { allUsers } from '../users/userCatalog';

export interface InvalidCredentials {
  readonly username: string;
  readonly password: string;
}

/** Generates random credentials for negative login tests, guaranteed not to collide with a real userCatalog account. */
export function createInvalidCredentials(): InvalidCredentials {
  let username: string;
  do {
    username = faker.internet.username();
  } while (allUsers.has(username));

  return { username, password: faker.internet.password({ length: 12 }) };
}
