#!/usr/bin/env bash
# End-to-end check of a running demo site: every page loads, and both example forms
# (Blazor with For, Razor Pages with asp-for tag helpers) validate, keep values and link errors.
#
#   tools/smoke-test.sh http://localhost:5000
set -euo pipefail
BASE="${1:-http://localhost:5000}"
JAR="$(mktemp)"
failures=0

check() { # description, haystack, needle
  if grep -qF -- "$3" <<<"$2"; then echo "ok    $1"; else echo "FAIL  $1 (missing: $3)"; failures=$((failures + 1)); fi
}

status() { curl -s -o /dev/null -w '%{http_code}' "$BASE$1"; }

post() { # path, form data
  rm -f "$JAR"
  local token
  token=$(curl -s -c "$JAR" "$BASE$1" | grep -o '__RequestVerificationToken"[^>]*value="[^"]*' | head -1 | sed 's/.*value="//')
  curl -s -b "$JAR" -X POST "$BASE$1" --data-urlencode "__RequestVerificationToken=$token" --data "$2"
}

for path in / /about /examples/form /examples/razor-pages /components/radios /components/date-input /preview/summary-list/0 /healthz; do
  code=$(status "$path"); [[ "$code" == 200 ]] && echo "ok    GET $path" || { echo "FAIL  GET $path returned $code"; failures=$((failures + 1)); }
done
code=$(status /no-such-page); [[ "$code" == 404 ]] && echo "ok    unknown page is 404" || { echo "FAIL  unknown page returned $code"; failures=$((failures + 1)); }

for stack in "Blazor:/examples/form:Model:_handler=appointment&" "Razor Pages:/examples/razor-pages:Details:"; do
  IFS=: read -r name path prefix extra <<<"$stack"
  html=$(post "$path" "${extra}${prefix}.FullName=&${prefix}.DateOfBirth.Day=1&${prefix}.DateOfBirth.Year=1990&${prefix}.NhsNumber=123&${prefix}.Needs=longer")
  check "$name: error summary links to the field" "$html" "href=\"#${prefix}.FullName\">Enter your full name</a>"
  check "$name: error summary links to a date's day" "$html" "href=\"#${prefix}.DateOfBirth-day\">Date of birth must include a month</a>"
  check "$name: error message wired to the input" "$html" "aria-describedby=\"${prefix}.FullName-error\""
  check "$name: typed value kept" "$html" "name=\"${prefix}.DateOfBirth.Year\" type=\"text\" value=\"1990\""
  check "$name: only the missing date part highlighted" "$html" "nhsuk-input--error nhsuk-input--width-2 nhsuk-date-input__input\" id=\"${prefix}.DateOfBirth-month\""
  check "$name: ticked checkbox kept" "$html" "value=\"longer\" checked"
  check "$name: label from [Display]" "$html" ">Full name</label>"

  html=$(post "$path" "${extra}${prefix}.FullName=Ada Lovelace&${prefix}.DateOfBirth.Day=10&${prefix}.DateOfBirth.Month=12&${prefix}.DateOfBirth.Year=1985&${prefix}.NhsNumber=9991234567&${prefix}.Contact=phone")
  check "$name: valid answers reach check your answers" "$html" "nhsuk-summary-list__value\">10 December 1985</dd>"
done

rm -f "$JAR"
if (( failures > 0 )); then echo "$failures check(s) failed"; exit 1; fi
echo "All smoke checks passed."
