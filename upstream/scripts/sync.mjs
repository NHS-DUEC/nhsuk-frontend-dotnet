// Syncs this port with the pinned nhsuk-frontend release.
//
//   npm run sync    copy assets + fixtures, regenerate C# from macro-options.json
//   npm run check   fail if the committed files are out of date (used in CI)
//
// Nothing here is hand-edited downstream: every output file is overwritten.

import fs from 'node:fs'
import path from 'node:path'
import { createRequire } from 'node:module'
import { fileURLToPath } from 'node:url'
import nunjucks from 'nunjucks'

const require = createRequire(import.meta.url)
const here = path.dirname(fileURLToPath(import.meta.url))
const upstreamDir = path.resolve(here, '..')
const repoRoot = path.resolve(upstreamDir, '..')
const checkOnly = process.argv.includes('--check')

const pkgDir = path.dirname(require.resolve('nhsuk-frontend/package.json'))
const version = JSON.parse(fs.readFileSync(path.join(pkgDir, 'package.json'), 'utf8')).version
const dist = path.join(pkgDir, 'dist')
const distNhsuk = path.join(dist, 'nhsuk')
const config = JSON.parse(fs.readFileSync(path.join(upstreamDir, 'port.config.json'), 'utf8'))
const ported = new Set(config.components)

const componentsProject = path.join(repoRoot, 'src/NhsukFrontend.Components')
const parityProject = path.join(repoRoot, 'src/NhsukFrontend.Parity')

// ---------------------------------------------------------------------------
// Output collection (so --check can compare without writing)

const outputs = new Map() // absolute path -> Buffer | string
const outputDirs = [] // directories fully owned by this script

function emit(file, content) { outputs.set(file, content) }
function ownDir(dir) { outputDirs.push(dir) }

// ---------------------------------------------------------------------------
// Naming helpers

const pascal = (s) => s.replace(/(^|[-_ ])(\w)/g, (_, __, c) => c.toUpperCase()).replace(/^\w/, (c) => c.toUpperCase())
const kebab = (s) => s.replace(/[A-Z]/g, (c) => '-' + c.toLowerCase())
const componentClass = (name) => 'Nhsuk' + pascal(name)
const optionsClass = (name) => pascal(name) + 'Options'
const xml = (s) => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
const csString = (s) => JSON.stringify(String(s)) // JSON string escapes are valid C#

// ---------------------------------------------------------------------------
// C# generation from macro-options.json

function loadOptions(component) {
  return JSON.parse(fs.readFileSync(path.join(distNhsuk, 'components', component, 'macro-options.json'), 'utf8'))
}

function generateComponent(component) {
  const nested = [] // extra classes emitted alongside
  const options = [...loadOptions(component), ...(config.additionalOptions?.[component] ?? [])]

  function typeFor(param, pathParts) {
    const key = [component, ...pathParts].join('/')
    const override = config.overrides[key] ?? {}
    let type
    if (override.type) return override.type + '?'
    // Some macro-options.json files declare `attributes` as a string; every template reads it as an object.
    if (param.name === 'attributes') return 'NhsukAttributes?'
    switch (param.type) {
      case 'string': type = 'string'; break
      case 'boolean': type = 'bool'; break
      case 'integer': type = 'int'; break
      case 'nunjucks-block': type = 'RenderFragment'; break
      case 'array':
        type = param.params
          ? `List<${nestedClass(param, pathParts, true)}?>`
          : 'List<string>'
        break
      case 'object':
        if (param.name === 'attributes') type = 'NhsukAttributes'
        else if (param.params) type = nestedClass(param, pathParts, false)
        else if (param.isComponent && ported.has(kebab(param.name))) type = optionsClass(kebab(param.name))
        else type = 'NhsukAttributes'
        break
      default:
        throw new Error(`Unhandled option type "${param.type}" at ${key}`)
    }
    if (override.list) type = `List<${type}?>`
    return type + '?'
  }

  function nestedClass(param, pathParts, isItem) {
    const name = pascal(component) + pathParts.map(pascal).join('') + (isItem ? 'Item' : 'Options')
    const key = [component, ...pathParts].join('/')
    if (!nested.some((n) => n.name === name)) {
      nested.push({ name, params: withAlias(param), pathParts, shorthand: config.overrides[key]?.shorthand, shorthandDefaults: config.overrides[key]?.shorthandDefaults })
    }
    return name
  }

  // An option with `alias` (for example date-input `items`, alias `input`) accepts every option of the
  // aliased component; its own `params` only add to or override them.
  function withAlias(param) {
    if (!param.alias || !fs.existsSync(path.join(distNhsuk, 'components', param.alias, 'macro-options.json'))) return param.params
    const own = new Set(param.params.map((p) => p.name))
    return [...param.params, ...loadOptions(param.alias).filter((p) => !own.has(p.name))]
  }

  function property(param, pathParts, { isParameter }) {
    const type = typeFor(param, pathParts)
    const lines = []
    lines.push(`    /// <summary>${xml(param.description)}</summary>`)
    lines.push(`    /// <remarks>Macro option <c>${xml(pathParts.join('.'))}</c>${param.required ? ' (required)' : ''}, released in ${xml(param.released)}.</remarks>`)
    if (param.deprecated) lines.push(`    [Obsolete("Deprecated in nhsuk-frontend ${param.deprecated}.")]`)
    const keepFalse = config.overrides[[component, ...pathParts].join('/')]?.keepFalse ? ', KeepFalse' : ''
    const attrs = isParameter
      ? `[Parameter, MacroOption(${csString(param.name)})${keepFalse}]`
      : `[JsonPropertyName(${csString(param.name)})${keepFalse}]`
    lines.push(`    ${attrs} public ${type} ${propName(param.name)} { get; set; }`)
    return lines.join('\n')
  }

  function propName(name) {
    return name === 'caller' ? 'ChildContent' : pascal(name)
  }

  function optionsClassBody(className, params, pathParts, shorthand, shorthandDefaults = {}) {
    const props = params
      .filter((p) => p.type !== 'nunjucks-block')
      .map((p) => property(p, [...pathParts, p.name], { isParameter: false }))
    const shorthandParam = shorthand ?? (params.some((p) => p.name === 'text') ? 'text' : null)
    const iface = shorthandParam ? `, IShorthandOptions<${className}>` : ''
    const lines = [`public sealed partial class ${className} : NhsukOptions${iface}`, '{']
    lines.push(props.join('\n\n'))
    lines.push('')
    lines.push('    /// <summary>Upstream templates accept <c>true</c> here to mean "use the defaults".</summary>')
    lines.push(`    public static implicit operator ${className}(bool value) => value ? new() { IsTrue = true } : null!;`)
    if (shorthandParam) {
      lines.push('')
      lines.push(`    /// <summary>Upstream templates accept a plain string here, used as <c>${shorthandParam}</c>.</summary>`)
      const extra = Object.entries(shorthandDefaults).map(([k, v]) => `, ${pascal(k)} = ${csString(v)}`).join('')
      lines.push(`    public static ${className} FromShorthand(string value) => new() { ${pascal(shorthandParam)} = value${extra}, IsShorthand = true };`)
      lines.push(`    public static implicit operator ${className}(string value) => FromShorthand(value);`)
    }
    lines.push('}')
    return lines.join('\n')
  }

  const cls = componentClass(component)
  const parameters = options.map((p) => property(p, [p.name], { isParameter: true }))
  const hasAttributes = options.some((p) => p.name === 'attributes')

  const body = []
  body.push(`/// <summary>Parameters for the NHS.UK frontend <c>${component}</c> component.</summary>`)
  body.push(`/// <remarks>Generated from <c>components/${component}/macro-options.json</c> in nhsuk-frontend ${version}.</remarks>`)
  body.push(`[MacroComponent(${csString(component)})]`)
  body.push(`public partial class ${cls}`)
  body.push('{')
  body.push(parameters.join('\n\n'))
  if (hasAttributes) {
    body.push('')
    body.push('    /// <summary>Razor convenience: any unmatched attribute is merged into <see cref="Attributes"/>.</summary>')
    body.push('    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }')
  }
  body.push('}')
  body.push('')
  body.push(`/// <summary>Options object for passing a <c>${component}</c> into another component.</summary>`)
  body.push(optionsClassBody(optionsClass(component), options, [], null))

  // Nested classes can themselves add nested classes, so drain the queue.
  for (let i = 0; i < nested.length; i++) {
    const n = nested[i]
    body.push('')
    body.push(`/// <summary>Nested options at <c>${component}.${n.pathParts.join('.')}</c>.</summary>`)
    body.push(optionsClassBody(n.name, n.params, n.pathParts, n.shorthand, n.shorthandDefaults))
  }

  return header() + body.join('\n') + '\n'
}

function header() {
  return [
    '// <auto-generated>',
    `//   Generated by upstream/scripts/sync.mjs from nhsuk-frontend ${version}.`,
    '//   Do not edit: run `npm run sync` in /upstream instead.',
    '// </auto-generated>',
    '#nullable enable',
    '#pragma warning disable CS0618 // deprecated upstream options are still generated',
    'using System.Text.Json.Serialization;',
    'using Microsoft.AspNetCore.Components;',
    'using NhsukFrontend.Components.Infrastructure;',
    '',
    'namespace NhsukFrontend.Components;',
    '',
    ''
  ].join('\n')
}

// ---------------------------------------------------------------------------
// Icons: render the upstream icon macro rather than hand-copying SVG paths

function generateIcons() {
  const iconSource = fs.readFileSync(path.join(distNhsuk, 'macros/icon.njk'), 'utf8')
  const names = [...iconSource.matchAll(/name == "([^"]+)"/g)].map((m) => m[1])
  const env = new nunjucks.Environment(new nunjucks.FileSystemLoader(dist))
  const entries = names.map((name) => {
    const html = env.renderString(`{% from "nhsuk/macros/icon.njk" import nhsukIcon %}{{ nhsukIcon("${name}") }}`, {})
    const inner = html.replace(/^[\s\S]*?<svg[^>]*>/, '').replace(/<\/svg>\s*$/, '').trim()
    return `        [${csString(name)}] = ${csString(inner)},`
  })
  return header() + [
    'internal static class IconPaths',
    '{',
    '    public static readonly IReadOnlyDictionary<string, string> Markup = new Dictionary<string, string>',
    '    {',
    ...entries,
    '    };',
    '}',
    ''
  ].join('\n')
}

// ---------------------------------------------------------------------------
// Page template: upstream ships no fixtures for template.njk, so render our own
// with the real template. `blocks` overrides {% block %} sections.

function renderTemplateFixtures() {
  const env = new nunjucks.Environment(new nunjucks.FileSystemLoader(dist))
  const cases = [
    { name: 'default', context: {}, blocks: { content: '<h1 class="nhsuk-heading-xl">Page heading</h1>' } },
    {
      name: 'with page variables',
      context: {
        htmlLang: 'cy',
        htmlClasses: 'app-html',
        htmlAttributes: { 'data-html': 'yes' },
        pageTitleLang: 'cy',
        themeColor: '#330072',
        assetPath: '/static/nhsuk',
        assetUrl: 'https://example.nhs.uk/static/nhsuk',
        bodyClasses: 'app-body',
        bodyAttributes: { 'data-body': 'yes' },
        cspNonce: 'abc123',
        containerClasses: 'app-container',
        mainClasses: 'app-main',
        mainLang: 'cy'
      },
      blocks: { content: '<p>Cynnwys</p>' }
    },
    {
      name: 'with Open Graph image URL',
      context: { opengraphImageUrl: 'https://example.nhs.uk/og.png' },
      blocks: {}
    },
    {
      name: 'with blocks',
      context: {},
      blocks: {
        pageTitle: 'Check your details – NHS App',
        head: '<link rel="stylesheet" href="/app.css">',
        bodyStart: '<div class="app-banner">Beta</div>',
        header: '<header class="app-header">Custom header</header>',
        beforeContent: '<a class="nhsuk-back-link" href="/">Back</a>',
        content: '<h1>Content</h1>',
        footer: '<footer class="app-footer">Custom footer</footer>',
        bodyEnd: '<p>End</p>'
      }
    },
    {
      name: 'with replaced main and skip link',
      context: {},
      blocks: { skipLink: '', main: '<main id="maincontent">Replaced</main>', headIcons: '' }
    }
  ]

  return {
    name: 'Page template',
    component: 'template',
    fixtures: cases.map(({ name, context, blocks }) => {
      const source = '{% extends "nhsuk/template.njk" %}' +
        Object.entries(blocks).map(([block, html]) => `{% block ${block} %}${html}{% endblock %}`).join('')
      return { name, context, blocks, html: env.renderString(source, context).trim() }
    })
  }
}

// ---------------------------------------------------------------------------
// Assemble outputs

// 1. Compiled assets (CSS, JS, images) straight from dist. Sass/JS are never ported.
const wwwroot = path.join(componentsProject, 'wwwroot')
ownDir(wwwroot)
for (const file of ['nhsuk-frontend.min.css', 'nhsuk-frontend.min.css.map', 'nhsuk-frontend.min.js', 'nhsuk-frontend.min.js.map']) {
  emit(path.join(wwwroot, file), fs.readFileSync(path.join(distNhsuk, file)))
}
for (const file of fs.readdirSync(path.join(distNhsuk, 'assets'), { recursive: true })) {
  const src = path.join(distNhsuk, 'assets', file)
  if (fs.statSync(src).isFile()) emit(path.join(wwwroot, 'assets', file), fs.readFileSync(src))
}

// 2. Generated C#
const generatedDir = path.join(componentsProject, 'Generated')
ownDir(generatedDir)
for (const component of ported) {
  emit(path.join(generatedDir, `${componentClass(component)}.g.cs`), generateComponent(component))
}
emit(path.join(generatedDir, 'IconPaths.g.cs'), generateIcons())
const headerTemplate = fs.readFileSync(path.join(distNhsuk, 'components/header/template.njk'), 'utf8')
const logoPath = headerTemplate.match(/<svg class="nhsuk-header__logo"[\s\S]*?<path fill="currentcolor" d="([^"]+)"/)?.[1]
if (!logoPath) throw new Error('Could not find the NHS logo path in header/template.njk – has the header markup changed?')
emit(path.join(generatedDir, 'UpstreamVersion.g.cs'), header() + [
  'public static class NhsukFrontendUpstream',
  '{',
  '    /// <summary>The nhsuk-frontend release this build was generated from.</summary>',
  `    public const string Version = ${csString(version)};`,
  '}',
  '',
  'internal static class NhsLogo',
  '{',
  '    /// <summary>SVG path for the NHS logo, taken from components/header/template.njk.</summary>',
  `    public const string Path = ${csString(logoPath)};`,
  '}',
  ''
].join('\n'))

// 3. Fixtures (rendered HTML for every upstream example) for parity checks and the demo
const fixturesDir = path.join(parityProject, 'Fixtures')
ownDir(fixturesDir)
const allComponents = fs.readdirSync(path.join(distNhsuk, 'components'))
  .filter((c) => fs.existsSync(path.join(distNhsuk, 'components', c, 'macro-options.json')))
for (const component of ported) {
  emit(path.join(fixturesDir, `${component}.json`), fs.readFileSync(path.join(distNhsuk, 'components', component, 'fixtures.json')))
}
emit(path.join(fixturesDir, 'template.json'), JSON.stringify(renderTemplateFixtures(), null, 2) + '\n')
emit(path.join(fixturesDir, '_manifest.json'), JSON.stringify({
  upstreamVersion: version,
  ported: [...ported, 'template'].sort(),
  notYetPorted: allComponents.filter((c) => !ported.has(c)).sort()
}, null, 2) + '\n')

// 4. Snapshot of every upstream macro-options.json, so an upgrade PR shows API changes as a diff
const snapshotDir = path.join(upstreamDir, 'snapshot')
ownDir(snapshotDir)
for (const component of allComponents) {
  emit(path.join(snapshotDir, `${component}.macro-options.json`), fs.readFileSync(path.join(distNhsuk, 'components', component, 'macro-options.json')))
}

// ---------------------------------------------------------------------------
// Write or check

function listFiles(dir) {
  if (!fs.existsSync(dir)) return []
  return fs.readdirSync(dir, { recursive: true })
    .map((f) => path.join(dir, f))
    .filter((f) => fs.statSync(f).isFile())
}

const stale = []
for (const dir of outputDirs) {
  for (const file of listFiles(dir)) {
    if (!outputs.has(file)) stale.push(`unexpected: ${path.relative(repoRoot, file)}`)
  }
}
for (const [file, content] of outputs) {
  const expected = Buffer.isBuffer(content) ? content : Buffer.from(content)
  if (!fs.existsSync(file) || !fs.readFileSync(file).equals(expected)) stale.push(`outdated: ${path.relative(repoRoot, file)}`)
}

if (checkOnly) {
  if (stale.length) {
    console.error(`Port is out of sync with nhsuk-frontend ${version}. Run \`npm run sync\` in /upstream.\n`)
    console.error(stale.slice(0, 50).join('\n'))
    process.exit(1)
  }
  console.log(`In sync with nhsuk-frontend ${version}.`)
} else {
  for (const dir of outputDirs) fs.rmSync(dir, { recursive: true, force: true })
  for (const [file, content] of outputs) {
    fs.mkdirSync(path.dirname(file), { recursive: true })
    fs.writeFileSync(file, content)
  }
  console.log(`Synced with nhsuk-frontend ${version}: ${ported.size} components ported, ${allComponents.length - ported.size} not yet ported.`)
  if (stale.length) console.log(`${stale.length} file(s) changed.`)
}

