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

`Program.cs` derives the app's supported cultures by scanning this folder for
`SharedResource.<culture>.json` files at startup - there is no separate list to edit. Adding a language
is dropping in the file:

1. Add `SharedResource.<culture>.json` here, either by hand or (preferably, for anything beyond a quick
   first draft) via [Hosted Weblate](https://hosted.weblate.org), which creates it from the English
   source and opens a pull request as translation progresses.
2. That's it. The next restart picks it up automatically - the language switcher
   (`CultureSelector.razor`), the `Admin/PlatformSettings` "Default language" setting, and the email
   template editor's translation dropdown all read the same scanned list.

Untranslated keys (missing entirely, or only partially translated) fall back to English - they never
show a raw key. See `Localization/JsonFileStringLocalizer.cs`'s remarks for exactly why: a key *is* its
own English fallback, by construction.

## Why JSON, not `.resx`

Every free translation-collaboration tool (Weblate included) speaks JSON as a first-class format;
`.resx` is Microsoft-specific and far less universally supported by that tooling. The backing
implementation is a small custom `IStringLocalizer` (`Localization/JsonFileStringLocalizer.cs`), not a
package - see its own remarks for why owning ~50 lines beat depending on one. Nothing about how a
`.razor` file consumes a translated string is non-standard, only where the values are stored.
