# Update sets — how the executable ATF records travel

The YAML specs in [`../`](../) are the *design* source of truth; the executable artifacts are
`sys_atf_*` records inside the instance. Update sets are how those records move between instances
and how a release's test state is archived. No XML is committed here on purpose: update-set XML is
an instance export, not an authorable format — hand-written XML would be untestable noise
pretending to be executable.

## Workflow

1. **Build in dev, inside a named update set** — `ATF <app> <release>`, e.g. `ATF CSM 2026-08`.
   Scope it to test records: the ATF tests, their suites, and nothing else. Application config under
   test travels in its *own* update set; pairing is by release name, so either side can be backed
   out independently.
2. **Complete + export** the update set (Export to XML) when the release's tests match the specs in
   git. Attach the export to the release ticket; the instance's retrieved-update-set history is the
   working archive.
3. **Import + preview + commit** on the next instance up (test/UAT). Preview problems on ATF records
   almost always mean a spec/instance drift — fix the drift, don't skip the record.
4. **Tag the git side**: the spec files' state at export time is the reviewable diff for "what
   changed in the tests this release". Update-set exports and git history stay in step because both
   are cut at the same release boundary.

## Conventions

- **One update set per app per release**, named `ATF <app> <release>`.
- **Copies of quick start tests live here too** — they're real records you maintain; the shipped
  ServiceNow originals are never edited and never exported.
- **Never mix** ATF records and application config in one update set.
- **Scoped-app alternative**: teams on source-controlled scoped apps can keep ATF tests in the app
  and version them through the platform's git integration instead — same principle, different
  vehicle; the specs here stay the design record either way.
