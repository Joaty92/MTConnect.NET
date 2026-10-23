# Contributing to MTConnect.NET

Thank you for taking the time to contribute — issues, discussions, and pull requests are all welcome. This file collects the mechanical checks a contributor is expected to run locally before pushing; the substantive discussion of what to work on lives on the [MTConnect.NET issue tracker](https://github.com/TrakHound/MTConnect.NET/issues) and on the [Documentation site](https://trakhound.github.io/MTConnect.NET/).

## Running the formatting gate locally

Every push to `master` and every non-draft pull request runs `dotnet format MTConnect.NET.sln --verify-no-changes` in CI (job `format`, defined in [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml)). The gate rejects any whitespace / indentation / brace-placement drift, so it is worth reproducing the check before pushing:

```bash
dotnet restore MTConnect.NET.sln
dotnet format MTConnect.NET.sln                                     # autofix in place
dotnet format MTConnect.NET.sln --verify-no-changes --verbosity diagnostic   # dry-run, same as CI
```

The two `dotnet new mtconnect.net-agent` template projects live outside the solution and CI verifies them in dedicated steps; run the same commands against them if you have touched files under `templates/`:

```bash
dotnet restore templates/mtconnect.net-agent/MTConnect-NET-Agent-Template.csproj
dotnet restore templates/mtconnect.net-agent/content/MTConnect.NET-Embedded-Agent/Agent.csproj
dotnet format templates/mtconnect.net-agent/MTConnect-NET-Agent-Template.csproj --verify-no-changes --verbosity diagnostic
dotnet format templates/mtconnect.net-agent/content/MTConnect.NET-Embedded-Agent/Agent.csproj --verify-no-changes --verbosity diagnostic
```

See [`docs/testing/workflows.md`](docs/testing/workflows.md#job-0--format) for the complete gate description, the `--severity warn` rationale, and the tracked follow-ups (root `.editorconfig` + `global.json` SDK pin).

## Running the test suite locally

The default sweep runs unit and light-integration tests across the whole solution:

```bash
./tools/test.sh          # Linux / macOS / Git Bash
./tools/test.ps1         # PowerShell, all platforms
```

Add `--e2e` / `-E2E` (Docker required) to also run the `Category=E2E` and `Category=RequiresDocker` workflow fixtures. `./tools/test.sh --help` (or `./tools/test.ps1 -?`) prints the full flag listing. The CI matrix and per-category filter rationale are documented on [`docs/testing/workflows.md`](docs/testing/workflows.md).

## Opening a pull request

- Keep the PR body focused on the user-facing diff — what changed, why it changed, and how a reviewer can verify it. Internal process detail belongs in the commit trailer or in follow-up comments, not in the description.
- Every push re-runs the full CI matrix on non-draft PRs. Leave a PR in draft while it is still being iterated; flip it to ready when it is ready for review.
- If your change touches the `templates/` tree, please also run the template-project format commands above — the CI gate will catch drift, but reproducing locally saves a red-CI round-trip.

Thanks again for your interest in improving MTConnect.NET.
