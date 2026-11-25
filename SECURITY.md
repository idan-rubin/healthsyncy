# Security Policy

## Supported versions

HealthSyncy has no versioned releases. Security fixes land on `main`, and the container images published from the latest `main` commit are the only supported build. If you run an instance, redeploy from a current commit to pick up fixes.

## Reporting a vulnerability

Use the repository’s [private vulnerability report form](https://github.com/idan-rubin/healthsyncy/security/advisories/new). The report goes to the maintainer.

Include what you found, how to reproduce it, and what an attacker could do with it. Never include real health data, device tokens or other secrets in a report.

This is a single-maintainer project, so reports are handled on a best-effort basis with no guaranteed response time.

## Scope

This policy covers the code in this repository. Each HealthSyncy instance is deployed and operated by its own owner, who is responsible for that deployment's configuration, credentials and data.
