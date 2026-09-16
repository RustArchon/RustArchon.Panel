// Copyright ©2026 Scott Blomfield

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace RustArchon.Panel;

/// <summary>
/// A personal local-dev convenience: sign straight into a named account with no password, when an
/// implementation actually exists. Deliberately split so the real logic can never be committed - see
/// SingleUserAuth.local.cs (gitignored via the <c>*.local.cs</c> pattern - see .gitignore's own
/// remarks). This half is safe to commit precisely because it does nothing by itself:
/// <see cref="MapEndpoint"/> is a partial method with no implementing declaration unless that
/// gitignored file exists, which the C# compiler turns into a silent no-op rather than a
/// missing-method build error.
/// </summary>
/// <remarks>
/// Exists because the real implementation was accidentally committed once already (see commit
/// 8aa63c9's own remarks) - a personal convenience toggle that leaked into a public repo's history.
/// <see cref="IsAllowed"/>'s two checks are what make that structurally hard to repeat even if the
/// gitignored file itself ever ended up inside a Docker build context by accident: every official
/// .NET base image (including the runtime-deps one this project's own Dockerfile uses) sets
/// <c>DOTNET_RUNNING_IN_CONTAINER</c> automatically, so anything built from that Dockerfile refuses
/// to activate regardless of what source made it into the image. Deliberately not
/// <c>Debugger.IsAttached</c> - that would also exclude Claude Code running this app via
/// <c>dotnet run</c>/a preview server to test a signed-in flow, which is a real use for this, not
/// just a Visual Studio convenience.
/// </remarks>
public static partial class SingleUserAuth
{
    /// <summary>
    /// Whether SingleUserAuth is even structurally allowed to activate here, regardless of whether an
    /// implementation exists. Checked independently by both call sites (this class's own
    /// <see cref="ApplyOnlyOutsideContainer"/> and App.razor's redirect trigger), so either one alone
    /// still refuses to activate even if the other somehow didn't.
    /// </summary>
    public static bool IsAllowed(IWebHostEnvironment environment) =>
        environment.IsDevelopment()
        && Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true";

    /// <summary>Maps the local-only login endpoint - implemented only in the gitignored half.</summary>
    static partial void MapEndpoint(WebApplication app);

    /// <summary>Maps the local-only login endpoint, if both <see cref="IsAllowed"/> agrees and an
    /// implementation of <see cref="MapEndpoint"/> actually exists on disk.</summary>
    public static void ApplyOnlyOutsideContainer(WebApplication app)
    {
        if (!IsAllowed(app.Environment))
        {
            return;
        }

        MapEndpoint(app);
    }
}
