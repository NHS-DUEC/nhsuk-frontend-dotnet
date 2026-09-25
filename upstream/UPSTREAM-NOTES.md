# Notes for nhsuk-frontend

Places where a component's `macro-options.json` doesn't describe what its `template.njk` actually reads, found
while porting nhsuk-frontend 10.6.0. Each one is worked around in `port.config.json`; if upstream fixes the
option file, the matching override can be deleted.

## Options the template reads but the option file doesn't declare

| Component | Option | Notes |
|---|---|---|
| character-count | `charactersUnderLimitText`, `charactersAtLimitText`, `charactersOverLimitText`, `wordsUnderLimitText`, `wordsAtLimitText`, `wordsOverLimitText` | Translation messages, used in fixtures |
| code | `caller` | The template supports a call block |
| contents-list | `items[].items` | Nested lists, used in fixtures |
| pagination | `items[].classes` | |
| password-input | `prefix`, `suffix` | Passed to the input; used in fixtures |
| scroll | `labelledBy` | Used by tables when `scroll` is set |
| search-input | `placeholder` | Used in fixtures |
| tabs | `classes`, `attributes` | |

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

A flag in `macro-options.json` for these would let every port handle them without reading templates.
