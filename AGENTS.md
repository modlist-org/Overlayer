# Overlayer Agent Guidelines

## 1. UI, Docs & Localization Tone
Applies to `Overlayer/Resource/Export/Lang/{en-US,ko-KR,zh-CN}.json`, `Lang/Setup/`, `Lang/Docs/`, `Overlayer/Setup/`, and `Overlayer/UI/Docs/`.

- **Voice:** High information density, detached, procedural formal style. Cut fillers, pleasantries, and exclamation marks (`그냥`, `너무`, `~해보세요`, `!`, `just`, `really`, `basically`, `you can`).
  - **KR:** Uncontracted formal written Korean (`~합니다`, `~됩니다`, `~며`, `~으나`, `~시, ...`). Prefer concrete, noun/procedure-centered phrasing over casual contractions (`해요`, `이런`, `-면서`).
  - **EN:** Crisp, objective, uncontracted register (`cannot`, `it is`, `does not`). State behavior directly.
- **No Trailing Period:** Omit a standalone final `.` (or `。`) at the very end of every UI/Docs/Setup string. Keep periods *between* sentences in multi-sentence text, and keep `..` / `...` untouched.
- **No Obvious Tooltips:** Do not attach tooltips to self-explanatory controls whose label already states their purpose (e.g., *Window Opacity*, *Tooltip Opacity*, *Grid Size*). Add tooltips only for non-obvious behavior, shortcuts, or constraints.
- **Exclusions:**
  - **Tutorial / Olly (`Overlayer/Tutorial/`, `Lang/Tutorial/`, `PRACTICE_*` keys):** Separate character voice—do not apply the dry UI/Docs tone here unless explicitly requested.
  - **Loading / progress strings (`...`):** Leave ellipses on loading indicators, action menus, and input placeholders as-is.
- **Localization Structure & Safety:**
  - `Translator` recursively loads `Lang/**/*.json`. Every language object requires `"0KTL": "DO_NOT_TRANSLATE_THIS_KEY!"`.
  - `Docs/`, `Setup/`, and `Tutorial/` keep English inline as fallback in C# and store `ko-KR.json` / `zh-CN.json` under `Overlayer/Resource/Export/Lang/<Feature>/`.
  - Keep 100% key and placeholder parity (`{0}`, `{1}`, `{WindowKey}`, `{HudKey}`) across `en-US`, `ko-KR`, and `zh-CN`.
  - **TMP Rich Text (`PageDocs`):** `DocSection.Title/Summary`, `DocParagraph`, and `DocCallout` are **not** auto-escaped—never put raw `<` or `>` in them (`DocTable`, `DocCode`, `DocBadges`, `DocLink` escape automatically).

## 2. Code Comments
- **Write comments only when necessary:** Do not narrate what the code already says.
- **What to comment:** Non-obvious invariants, Unity/threading constraints (e.g., off-main-thread execution), IL2CPP/Mono differences, or *why* a workaround exists.
- **Style:** Brief (1–2 lines), direct, no filler. Match surrounding code style (concise English).

## 3. Build & Verification
- Build mod + export `Resource/Export` (including `Lang/`): `./build.sh Release_ML`
- Run unit tests (no game DLLs required): `./test.sh`
