"""Single source of truth for how many scenarios run concurrently.

Must stay in sync with `saucedemo_uitests.users.user_catalog.POOL_USERS` (one distinct login user per
parallel worker) and with the `-n` value passed to `pytest-xdist` in scripts/CI - unlike the two other
frameworks in this repo, there is no compile-time-constant attribute to thread this value into (NUnit's
`[assembly: LevelOfParallelism(...)]`, Cucumber.js's `cucumber.js` config), so keeping this in sync is
entirely a matter of convention, enforced only by `assigned_user.py`'s own runtime assertion.
"""

MAX_PARALLEL_WORKERS = 3
