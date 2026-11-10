[ok] AI agent docs installed -> AGENTS.md, GEMINI.md

  Tip: `pnpm exec astryx init --all` also points you to the theme and page-building workflows.

  Next steps:
    1. Ensure the @stylexjs/stylex peer dependency is met
       (run `pnpm exec astryx doctor` to verify)
    2. Import base styles: import '@astryxdesign/core/reset.css'
       and import '@astryxdesign/core/astryx.css'
    3. Import components: import { Button } from '@astryxdesign/core/Button'
    4. Add and wire a theme:
       npm install @astryxdesign/theme-neutral
       pnpm exec astryx theme add neutral
       import { Theme } from '@astryxdesign/core/theme'
       import { themes, defaultThemeSlug } from './astryx-themes'
       <Theme theme={themes[defaultThemeSlug]}>...</Theme>
       Extend an imported theme to customize it. Run `pnpm exec astryx theme eject <slug>` only to fork source.
    5. pnpm exec astryx --help for all commands
