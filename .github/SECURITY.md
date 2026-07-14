# Security Policy

## Supported Versions

Callora is under active development. Security fixes are applied to the current
state of the `main` branch and the latest release. Please run the most recent
version before reporting an issue.

| Version         | Supported          |
| --------------- | ------------------ |
| `main` (latest) | :white_check_mark: |
| Older commits   | :x:                |

## Reporting a Vulnerability

**Please report security vulnerabilities privately — do not open a public
issue, pull request, or discussion for them.**

Preferred channel: GitHub's private vulnerability reporting for this
repository —
[Report a vulnerability](https://github.com/BechsteinDigital/Callora/security/advisories/new)
(repository → **Security** → **Advisories** → **Report a vulnerability**).

Alternatively, email **info@bechstein.digital** with the subject prefix
`[SECURITY]` and include:

- a description of the issue and its impact,
- steps to reproduce (a proof of concept if possible),
- the affected components and versions/commit,
- any suggested remediation, if you have one.

## What to Expect

- **Acknowledgement** within 3 business days.
- An assessment and, if confirmed, a planned fix with a target timeline.
- Progress updates until the issue is resolved.
- Coordinated disclosure: we ask that you keep the report confidential until a
  fix is released, and we will credit you in the advisory unless you prefer to
  remain anonymous.

## Scope

In scope: the Callora host platform and the first-party plugins in this
repository (authentication and RBAC, plugin runtime and isolation, webhook
egress, data handling, and the HTTP/API surface).

Out of scope: findings that require a compromised host or operator account,
issues in third-party dependencies without a demonstrated impact on Callora,
and reports produced solely by automated scanners without a working proof of
concept.

## Hardening Notes

Callora is designed to fail closed: missing secrets abort startup outside
development, workspace access is denied by default, and plugin routes cannot
overlay host endpoints. When deploying, always override the development
defaults (JWT signing key, database credentials, bootstrap API key, and the
demo administrator) with strong, unique values.
