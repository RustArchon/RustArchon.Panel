// Copyright ©2026 Scott Blomfield

namespace RustArchon.Panel.Localization;

/// <summary>
/// A marker type only - never instantiated, never given members. Its sole purpose is to be the type
/// parameter of <c>IStringLocalizer&lt;SharedResource&gt;</c>, so every page and component shares one
/// resource file (<c>Resources/SharedResource.*.json</c>) instead of each generating its own
/// per-component resource file the way <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/>'s
/// usual per-type convention would.
/// </summary>
/// <remarks>
/// Deliberate: a translator working through Weblate needs one file per language to translate, not one
/// per `.razor` file in the app. The "shared resource" pattern is the standard way to get that with
/// the standard <c>IStringLocalizer&lt;T&gt;</c> API - see
/// <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization#shared-resources"/>.
/// </remarks>
public sealed class SharedResource;
