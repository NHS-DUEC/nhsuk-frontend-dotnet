# Notes for nhsuk-frontend

Places where a component's `macro-options.json` doesn't describe what its `template.njk` actually reads, found
while porting nhsuk-frontend 10.6.0 and still present in 10.6.1. Each one is worked around in `port.config.json`; if upstream fixes the
option file, the matching override can be deleted.

## Options the template reads but the option file doesn't declare

| Component | Option | Notes |
|---|---|---|
| contents-list | `items[].items` | Nested lists, used in fixtures |
| pagination | `items[].classes` | |

10.6.1 declared the character count messages, code `caller`, password input `prefix`/`suffix`, scroll
`labelledBy`, search input `placeholder` and tabs `classes`/`attributes`, so those workarounds were removed.

## Options declared with too narrow a shape

| Component | Option | Declared as | Template reads |
|---|---|---|---|
| code, password-input, search-input | `button` | a few properties | every button option (text, ariaLabel, icon, disabled, preventDoubleClick…) |
| hero | `content` | an object | an object or a list of objects |
| hero | `content.image` | an object | every images component option |
| summary-list | `rows[].attributes` (and key, value, actions) | `string` | an object, like every other `attributes` |
| tables | `rows` | a list of cells | a list of rows, each a list of cells |
| task-list | `items[].title` | an object | the same shape as `items[].heading` (deprecated form) |

## `false` is different from "not set"

These templates use Nunjucks' `default` filter, so `false` means "leave it out" while a missing value means "use the
default". A typed port needs to know which options behave this way:

- date-input `day`, `month`, `year`
- button `icon` (search input passes `icon: false` for a text-only button)
- search-input `button`
- label and legend `heading` (`isPageHeading: true` with `heading: false` means no heading)
- tag `colour` (`colour: false` adds `nhsuk-tag--no-colour`; pass the string `"false"` from C#)

A flag in `macro-options.json` for these would let every port handle them without reading templates.
