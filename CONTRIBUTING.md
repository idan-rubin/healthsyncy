# Contributing

HealthSyncy is a small, single-maintainer project. Each instance serves one person and one iPhone. Bug reports and focused pull requests are welcome.

## Before you start

- For anything larger than a bug fix, open an issue first so the approach is agreed before you write code.
- Report security problems privately, as described in [SECURITY.md](SECURITY.md). Do not open a public issue for them.
- Never put real health data, device tokens or other secrets in issues, tests or commits. The checks use generated keys and synthetic metrics.

## Build and test

You need a .NET 11 SDK, Docker, Bash 4 or newer, OpenSSL, Python 3 and curl. From the repository root:

```sh
dotnet build HealthSyncy.slnx -c Release
checks/mongo.sh
python3 checks/manifests.py
```

The same commands run on every pull request. The iPhone companion builds separately on macOS with Xcode and the .NET iOS workload; the exact steps are in `.github/workflows/ci.yml`.

## Pull requests

- Keep each pull request to one change, with a short title that says what it does.
- The product code is C#. Deployment and CI tooling lives in `scripts/` and `checks/`.
- Update the README when behaviour, configuration or deployment steps change.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
