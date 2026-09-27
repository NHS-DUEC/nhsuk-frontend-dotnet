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

## Pull requests

Keep each pull request to one change, say what it fixes, and make sure CI passes. A maintainer will review it.
