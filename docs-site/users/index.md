# User Guide

This guide is for the people who **run** Callora: platform operators and
workspace administrators. It is task-oriented — how to sign in, manage tenants
and workspaces, install and activate plugins, enable communication features, and
keep the platform healthy day to day.

If you are building plugins, see the
[API Reference](/api/) instead; this guide does not cover plugin
development.

## What Callora is

Callora is a domain-neutral .NET plugin platform. The **host** is a pure
platform: authentication and RBAC, user and plugin management, a business-event
bus, and dynamic plugin routing. Everything domain-specific — voice, dialing,
call routing — lives in **plugins**. As an operator you spend most of your time
in the admin shell served at `/admin`.

## Sections

- **[Getting Started](getting-started.md)** — first run: how the platform is
  started and reached, the bootstrap operator and password policy, signing in,
  and the admin shell layout with its workspace switcher.
- **[Administration](administration.md)** — the admin shell in depth: RBAC
  (SuperAdmin vs. Admin), tenants, users and members, plugin install/activate,
  and system configuration.
- **[Workspaces & Surfaces](workspaces-surfaces.md)** — the three orthogonal
  axes (tenant / workspace / surface), creating workspaces, defining surfaces
  and their access modes, and per-workspace themes and branding.
- **[Communication (VoIP)](communication.md)** — the flagship Communication
  plugin: what it does, where it appears for end users, and how it is enabled
  per workspace.
- **[Flows](flows.md)** — low-code automation: Rules and Flows, the call-routing
  use case, and where they are managed.
- **[Operations](operations.md)** — day-2 concerns: background jobs, webhooks,
  monitoring and SLOs, and rate limiting.

## A note on scope

Callora is under active development. Where a feature is planned but not yet
fully implemented, this guide marks it with a **Status** note rather than
describing behavior that does not exist yet.
