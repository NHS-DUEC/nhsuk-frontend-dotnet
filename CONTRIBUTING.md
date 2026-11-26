# Contributing

Thanks for helping. nhsuk-frontend-dotnet is a community project: it is not maintained by NHS England or the
NHS design system team, and it is looked after by volunteers on a best-efforts basis.

## Where to raise things

- **The HTML is different from nhsuk-frontend, or a component option doesn't work** → [open an issue here](../../issues/new/choose).
- **A problem with a component's design, content or accessibility itself** → that belongs upstream, in
  [nhsuk-frontend](https://github.com/nhsuk/nhsuk-frontend/issues) or the
  [NHS design system community](https://service-manual.nhs.uk/community-and-contribution). Once fixed there,
  the fix reaches this port with the next release.
- **A security vulnerability** → don't open an issue; see [SECURITY.md](SECURITY.md).

## The one rule

**The HTML must match upstream exactly.** Every change is checked against all of nhsuk-frontend's own examples,
through both the Razor components and the tag helpers. If you need different markup, the change belongs
upstream, not here.

## Making a change

You need the .NET 8 SDK, and Node.js 22 if you touch anything under `upstream/`.

```sh
dotnet build NhsukFrontend.sln
dotnet test NhsukFrontend.sln
dotnet run --project src/NhsukFrontend.Demo        # the demo site, with every example
```

- **Fixing a component:** edit its `.razor` file in `src/NhsukFrontend.Components/Components/`, then run
  `dotnet run --project tools/NhsukFrontend.ParityCheck -- <component> -v` for a readable diff against upstream.
- **Never edit files under `Generated/`.** They come from upstream's `macro-options.json`. To change what is
  generated, edit `upstream/port.config.json` or `upstream/scripts/sync.mjs`, then run `npm run sync` in `upstream/`.
- **Upstream releases** are picked up automatically by a scheduled workflow, which opens a pull request with a
  report. Please don't update the `nhsuk-frontend` version by hand.
- Found a gap in upstream's option files? Add it to `upstream/UPSTREAM-NOTES.md` as well as working around it.

The README explains how the port works in more detail.

## Releasing

Publishing a GitHub release publishes the package to [nuget.org](https://www.nuget.org/packages/nhsuk-frontend-dotnet)
(`.github/workflows/publish.yml`).

1. Set the version. It is the upstream version plus `VersionSuffix` in
   `src/NhsukFrontend.Components/NhsukFrontend.Components.csproj`: raise the suffix (`beta.2`) for another release
   of the port against the same upstream version, and reset it to `beta.1` when upstream moves on. A version on
   nuget.org can never be replaced, so every release needs a new one.
2. Merge to `main` and wait for CI.
3. On GitHub, create a release whose tag is the version with a `v` in front, such as `v10.7.2-beta.2`. The workflow
   tests, packs, checks the tag against the package version and pushes. The package is installable once nuget.org
   has validated it, usually within the hour.

One-off setup, by a member of the nuget.org organisation that owns the package:

- On nuget.org, under your username, choose Trusted Publishing and add a policy owned by the organisation, with
  repository owner `NHS-DUEC`, repository `nhsuk-frontend-dotnet` and workflow file `publish.yml`. Leave the
  environment empty.
- On GitHub, add a repository secret `NUGET_USER` holding that person's nuget.org username (not their email).

The policy stops working if that person leaves the nuget.org organisation, until someone else creates one and
updates `NUGET_USER`. If the repository moves or is renamed, update the policy to match.

## Pull requests

Keep each pull request to one change, say what it fixes, and make sure CI passes. A maintainer will review it.
