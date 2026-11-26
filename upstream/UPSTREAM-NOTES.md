# Notes for nhsuk-frontend

Places where a component's `macro-options.json` doesn't describe what its `template.njk` actually reads, found
while porting nhsuk-frontend 10.6.0 and still present in 10.7.2. Each one is worked around in `port.config.json`; if upstream fixes the
option file, the matching override can be deleted.

## Options the template reads but the option file doesn't declare

| Component | Option | Notes |
|---|---|---|
| contents-list | `items[].items` | Nested lists, used in fixtures |
| pagination | `items[].classes` | |
| visually-hidden | `placement` | Read by `nhsukContent` (`"before"`, `"start"`, `"end"`, `"after"`), used in link fixtures |

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
- environment-banner `tag` (10.7.0: `tag: false` leaves the tag out; a missing tag is "Development")
- button, heading and link `className` (10.7.0: `className: false` or `""` means no base class; pass `"false"` or `""` from C#)

A flag in `macro-options.json` for these would let every port handle them without reading templates.

## Shared shapes from 10.7.0

10.7.0 moved most templates onto shared macros. The port mirrors them in `Infrastructure/Render.cs`, so a change
to one of these upstream is usually a change in one place here:

| Upstream | Port |
|---|---|
| `macros/content.njk` (`nhsukContent`) | `Render.Content` |
| `macros/slot.njk` (`nhsukSlot`) | `Render.Slot`, with every `slots` option typed as `NhsukSlots` |
| `components/link/template.njk` | `Render.Link` (also behind `NhsukLink`, back link, action link, card actions…) |
| `components/visually-hidden/template.njk` | `Render.VisuallyHidden` |

Things a typed port can't copy exactly:

- **Text as a list or number.** `nhsukContent` joins a list of text with commas and prints numbers. Option files
  read from JSON accept both; from C# pass a string.
- **Environment banner `%{name}`.** Upstream replaces the placeholder in the rendered call block. The port
  replaces it in child content made of text and markup; child content containing other components is left as it is.
- **Items that alias the link component** (card actions, header, footer, breadcrumb and contents list items)
  declare every link option, but each template only passes some of them on. Only the ones the template passes
  have an effect, here as upstream.

