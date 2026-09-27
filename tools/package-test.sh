#!/usr/bin/env bash
# Packs the library, installs the package into a brand-new Razor Pages app the way a team would,
# publishes it, and checks the tag helpers render and the CSS, JavaScript and images are served in Production.
#
#   tools/package-test.sh
#   NUGET_ORG_SOURCE= PACK_ARGS=--no-restore tools/package-test.sh   # offline, after a normal restore
set -euo pipefail
trap 'echo "FAIL  line $LINENO: $BASH_COMMAND (exit $?)"' ERR
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK="$(mktemp -d)"
PORT="${PORT:-5099}"
trap '[[ -n "${APP_PID:-}" ]] && kill "$APP_PID" 2>/dev/null; rm -rf "$WORK"' EXIT
# A private package cache, so the freshly built package is always used, never an older copy of the same version.
export NUGET_PACKAGES="$WORK/nuget-cache"

dotnet pack "$ROOT/src/NhsukFrontend.Components" -c Release -o "$WORK/packages" -v q ${PACK_ARGS:-}
cd "$WORK"
echo "Using .NET SDK $(dotnet --version)"
dotnet new webapp -n TrialService -o app --no-restore > /dev/null
cd app
# Our package only ever comes from the fresh build; anything the .NET SDK itself needs comes from nuget.org.
# NUGET_ORG_SOURCE= (empty) leaves nuget.org out, for machines without internet access.
NUGET_ORG="${NUGET_ORG_SOURCE-https://api.nuget.org/v3/index.json}"
{
  echo '<configuration>'
  echo '  <packageSources>'
  echo '    <clear />'
  echo "    <add key=\"local\" value=\"$WORK/packages\" />"
  [[ -n "$NUGET_ORG" ]] && echo "    <add key=\"nuget.org\" value=\"$NUGET_ORG\" />"
  echo '  </packageSources>'
  echo '  <packageSourceMapping>'
  echo '    <packageSource key="local"><package pattern="nhsuk-frontend-dotnet" /></packageSource>'
  [[ -n "$NUGET_ORG" ]] && echo '    <packageSource key="nuget.org"><package pattern="*" /></packageSource>'
  echo '  </packageSourceMapping>'
  echo '</configuration>'
} > nuget.config
dotnet add package nhsuk-frontend-dotnet --prerelease
# Use the template's own Program.cs, whichever .NET version made it, adding just the one setup line.
# The official templates use Windows line endings, so normalise them first or the anchors below never match.
sed -i 's/\r$//' Program.cs
grep -q '^var builder = WebApplication.CreateBuilder(args);$' Program.cs || { echo "FAIL  unexpected Program.cs template"; cat Program.cs; exit 1; }
sed -i 's|^var builder = WebApplication.CreateBuilder(args);$|&\nbuilder.Services.AddNhsukFrontend();|' Program.cs
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
for _ in $(seq 1 60); do curl -sf -o /dev/null "http://127.0.0.1:$PORT/" && break; sleep 1; done
curl -sf -o /dev/null "http://127.0.0.1:$PORT/" || { echo "FAIL  the app did not start or its home page errored"; cat ../app.log; exit 1; }

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
