// Copyright ©2026 Scott Blomfield

using Microsoft.AspNetCore.Components.Web;

namespace RustArchon.Panel.Components;

/// <summary>
/// Shared, stable render mode instances - never construct <c>new InteractiveServerRenderMode(...)</c>
/// inline in a component's markup or <c>@rendermode</c> directive. <see cref="InteractiveServerRenderMode"/>
/// doesn't override equality, so Blazor's render-mode-boundary reconciliation compares by reference -
/// a fresh instance allocated on every render (or every navigation, for a page-level directive) looks
/// like "a different render mode" each time, needlessly tearing down and recreating that component's
/// whole interactive island (and, with it, its DI scope) instead of reusing the existing one. Confirmed
/// by direct tracing on <c>MainLayout</c>'s own <c>TenantSwitcher</c> usage - not theoretical.
/// </summary>
public static class RenderModes
{
    public static readonly InteractiveServerRenderMode NonPrerenderedServer = new(prerender: false);
}
