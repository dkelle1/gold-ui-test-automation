"""Generates random credentials for negative login scenarios, guaranteed not to collide with a real
`user_catalog` account."""

from faker import Faker

from saucedemo_uitests.users.user_catalog import ALL_USERS


def create_invalid_credentials() -> tuple[str, str]:
    fake = Faker()

    username = fake.user_name()
    while username in ALL_USERS:
        username = fake.user_name()

    password = fake.password(length=12)
    return username, password
