# Contributing

HealthSyncy is a small, single-maintainer project. Each instance serves one person and one iPhone. Bug reports and focused pull requests are welcome.

## Before you start

- For anything larger than a bug fix, open an issue first so the approach is agreed before you write code.
- Report security problems privately, as described in [SECURITY.md](SECURITY.md). Do not open a public issue for them.
- Never put real health data, device tokens or other secrets in issues, tests or commits. The checks use generated keys and synthetic metrics.

## Build and test

You need a .NET 11 SDK, Docker, Bash 4 or newer, OpenSSL, Python 3 and curl. Run these commands from the repository root.

```sh
dotnet build HealthSyncy.slnx -c Release
dotnet test --solution HealthSyncy.slnx -c Release --no-build
checks/mongo.sh
python3 checks/manifests.py
```

The unit tests need no database or network. They live in `tests/unit`, one project per domain, and use xUnit, FluentAssertions and AutoMocker.

On macOS, the integration script runs the .NET services and temporary OAuth issuer in a Linux test container. Docker must be running. The fixture needs no changes to your Mac’s trusted certificates. On Linux, use `checks/mongo.sh --container` to exercise the same path.

The integration suite starts a disposable MongoDB, exercises storage permissions and conflicting writes, then sends synthetic uploads and authenticated REST and MCP requests.

Pull requests run the build, the rule-set check, the unit tests with a coverage gate, the containerized integration suite and the manifest checks. The iPhone companion builds separately on macOS. See the [device build guide](docs/iphone.md) or the [simulator checks in CI](.github/workflows/ci.yml).

## Rules and coverage

CI compiles the solution and the iPhone app with the rules in `.editorconfig.ci`. Analyzer warnings fail the build. To check the solution locally, run these commands.

```sh
cp .editorconfig.ci .editorconfig
dotnet build HealthSyncy.slnx -c Release
dotnet format HealthSyncy.slnx --verify-no-changes --no-restore
git checkout .editorconfig
```

Pull requests fail below 90% line coverage. To measure it locally, run these commands. The first one clears reports from earlier runs.

```sh
rm -rf TestResults
dotnet test --solution HealthSyncy.slnx -c Release --coverage --coverage-output-format cobertura --results-directory "$PWD/TestResults"
python3 checks/coverage.py TestResults 90
```

The figure covers the hand-written code in every project except the iPhone entry point. Its UIKit, HealthKit and keychain code runs only on a device, so keep rules out of it and in `HealthSyncy.Mobile.Domain`.

## Pull requests

- Keep each pull request to one change, with a short title that says what it does.
- The product code is C#. Deployment and CI tooling lives in `scripts/` and `checks/`.
- Follow [the code layout](docs/architecture.md#how-the-code-is-organised). Put a rule in a `Domain` project behind an interface, keep the entry point thin, and add unit tests to the matching project in `tests/unit`.
- Update the README or the relevant guide in `docs/` when behavior, configuration or deployment steps change.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
