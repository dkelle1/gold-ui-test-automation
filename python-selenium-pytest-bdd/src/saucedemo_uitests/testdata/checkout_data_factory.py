"""Generates realistic checkout form data with Faker. Login credentials are never generated this way -
see `users.user_catalog` - only data the app accepts freely.

Builds a fresh `Faker` instance on every call rather than sharing one at module scope: unlike the C# and
TS siblings' thread-based parallelism, pytest-xdist workers are separate processes (see
`users.assigned_user`), so there is no in-process concurrent access to guard against here - a fresh
instance is simply the easiest way to make the optional `seed` parameter instance-local (via
`seed_instance`) without ever touching `Faker.seed`, which reseeds the shared generator *every* Faker
instance draws from (confirmed directly: it is a classmethod, the exact global-mutable-state trap the C#
sibling's own comment warns about for `Bogus.Randomizer.Seed`).
"""

from faker import Faker

from saucedemo_uitests.testdata.checkout_info import CheckoutInfo


def create_checkout_info(seed: int | None = None) -> CheckoutInfo:
    fake = Faker()
    if seed is not None:
        fake.seed_instance(seed)

    return CheckoutInfo(
        first_name=fake.first_name(),
        last_name=fake.last_name(),
        postal_code=fake.zipcode(),
    )
