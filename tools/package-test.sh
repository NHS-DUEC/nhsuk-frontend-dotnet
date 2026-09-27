#!/usr/bin/env bash
# Packs the library, installs the package into a brand-new Razor Pages app the way a team would,
# publishes it, and checks the tag helpers render and the CSS, JavaScript and images are served in Production.
#
#   tools/package-test.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK="$(mktemp -d)"
PORT="${PORT:-5099}"
trap 'kill "${APP_PID:-0}" 2>/dev/null || true; rm -rf "$WORK"' EXIT

dotnet pack "$ROOT/src/NhsukFrontend.Components" -c Release -o "$WORK/packages" -v q ${PACK_ARGS:-}
cd "$WORK"
echo "Using .NET SDK $(dotnet --version)"
dotnet new webapp -n TrialService -o app --no-restore > /dev/null
cd app
cat > nuget.config <<XML
<configuration>
  <packageSources>
    <!-- Only the freshly built package, so the test can't pick up a published version by mistake.
         The package has no NuGet dependencies; add nuget.org here if that ever changes. -->
    <clear />
    <add key="local" value="$WORK/packages" />
  </packageSources>
</configuration>
XML
dotnet add package nhsuk-frontend-dotnet --prerelease > /dev/null
# Our own Program.cs rather than editing the template's, which differs between .NET versions
# (from .NET 9 it uses MapStaticAssets instead of UseStaticFiles).
cat > Program.cs <<'CS'
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
var app = builder.Build();
app.UseNhsukFrontendAssets();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.Run();
CS
printf '@addTagHelper *, NhsukFrontend.Components\n@using NhsukFrontend.Components\n' >> Pages/_ViewImports.cshtml
cat > Pages/Index.cshtml <<'CSHTML'
@page
@{ Layout = null; }
<!DOCTYPE html>
<html lang="en">
<head><nhsuk-frontend-styles /></head>
<body>
  <nhsuk-panel heading="Installed from the package" text="It works." />
  <nhsuk-frontend-scripts />
</body>
</html>
CSHTML
# Publish and run in Production, as a real deployment would (packaged files are copied into the app on publish).
dotnet publish -c Release -o published -v q
cd published
ASPNETCORE_URLS="http://127.0.0.1:$PORT" ASPNETCORE_ENVIRONMENT=Production dotnet TrialService.dll > ../app.log 2>&1 &
APP_PID=$!
for _ in $(seq 1 30); do curl -sf -o /dev/null "http://127.0.0.1:$PORT/" && break; sleep 1; done

html=$(curl -sf "http://127.0.0.1:$PORT/")
grep -q 'class="nhsuk-panel"' <<<"$html" || { echo "FAIL  the panel tag helper did not render"; cat ../app.log; exit 1; }
CSS=/_content/NhsukFrontend.Components/nhsuk-frontend.min.css
JS=/_content/NhsukFrontend.Components/nhsuk-frontend.min.js
grep -qF "href=\"$CSS\"" <<<"$html" || { echo "FAIL  <nhsuk-frontend-styles /> did not link $CSS"; exit 1; }
grep -qF "$JS" <<<"$html" || { echo "FAIL  <nhsuk-frontend-scripts /> did not load $JS"; exit 1; }
for path in "$CSS" "$JS" /assets/images/favicon.ico; do
  code=$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$PORT$path")
  [[ "$code" == 200 ]] && echo "ok    $path" || { echo "FAIL  $path returned $code"; cat ../app.log; exit 1; }
done
echo "The package installs and works in a new Razor Pages app."
