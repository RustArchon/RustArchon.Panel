# Translations

`SharedResource.en-US.json` is the source of truth for every user-facing string in the Panel that has
been converted to go through `IStringLocalizer<SharedResource>` (see
`RustArchon.Panel/Localization/SharedResource.cs`). It is **one shared file for the whole app**, not
one per page or component - that's deliberate, so a translator working through Weblate has one file to
translate per language, not dozens.

## Adding a string

1. In the `.razor` file, replace the literal text with `@Localizer["The exact English text"]` (inject
   `IStringLocalizer<SharedResource> Localizer`).
2. Add the same key to `SharedResource.en-US.json`, with the key and value identical - the English file
   is both the source strings *and* the translation for English.
3. Nothing else. A new language's file appears the same way - `SharedResource.<culture>.json` - either
   added directly or pulled in via Weblate below.

## Adding a language

Translations are managed in [Hosted Weblate](https://hosted.weblate.org), not by hand-editing JSON
files in this repo directly (Weblate does that on your behalf, via pull request). To add a new
language:

1. Add the culture to `SupportedCultures/SupportedUICultures` in `RustArchon.Panel/Program.cs`'s
   `RequestLocalizationOptions`, so the app and its language switcher actually offer it.
2. Add the language in Weblate's project settings - it creates `SharedResource.<culture>.json` from the
   English source automatically.
3. Once translated (fully or partially - untranslated keys fall back to English, they never show a raw
   key), Weblate opens a pull request adding/updating that file. Merging it is the entire deploy step.

## Why JSON, not `.resx`

Every free translation-collaboration tool (Weblate included) speaks JSON as a first-class format;
`.resx` is Microsoft-specific and far less universally supported by that tooling. The backing library
is [AspNetCore.Localizer.Json](https://github.com/AskmethatFR/AspNetCore.Localizer.Json), plugged into
the standard `Microsoft.Extensions.Localization`/`IStringLocalizer<T>` pipeline - nothing about how a
`.razor` file consumes a translated string is non-standard, only where the values are stored.
