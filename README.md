# RustArchon.Panel

The actual RustArchon product: sign-up, login, server registration, the always-on RCON console, and
invitation-code administration - served from `panel.rustarchon.com`. Owns the ASP.NET Core Identity
database; everything else (servers, tenants, roles) is reached through generated API clients calling
[RustArchon.Api](https://github.com/RustArchon/RustArchon.Api). The marketing site
([RustArchon.Web](https://github.com/RustArchon/RustArchon.Web)) is a separate app entirely - this
repo has no marketing pages of its own.

Part of the [RustArchon](https://github.com/RustArchon/RustArchon) system - see that repo for the
full architecture and how to run the whole stack locally or via Docker Compose.

## Key files

- `Program.cs` - Identity/cookie/Data Protection setup, JWT token-exchange wiring, and every
  generated API client this app calls. Almost everything else in this repo hangs off something
  registered here.
- `Components/Account/**` - the full ASP.NET Core Identity Blazor template (login, register,
  2FA, passkeys, account management) plus this project's own additions
  (`IdentityRedirectManager`, `NewTenantBootstrapper` usage in `Register.razor`/`ExternalLogin.razor`).
- `Components/Pages/Servers/{ServersList,ServerDetail}.razor` - the actual product surface: register
  a server, view its console.
- `Components/Pages/Admin/InvitationCodes.razor` - platform-admin invitation-code management,
  gated by the `PlatformAdmin` policy (a config email allow-list, not a database role).
- `Services/QueuedEmailSender.cs` - `IEmailSender<ApplicationUser>` that publishes to
  RustArchon.Api's `/internal/email` endpoint rather than sending anything itself - see the root
  README's "Before deploying this anywhere real" section.
- `Data/ApplicationDbContext.cs`, `Migrations/` - the Identity database this app owns exclusively.

## Cross-app session (SSO)

This is the app that actually creates and destroys sessions - RustArchon.Web only reads the cookie
this issues, read-only, to decide whether to show "Dashboard" or "Log In"/"Sign Up". See the
[umbrella repo's README](https://github.com/RustArchon/RustArchon#cross-app-session-sso) for the
full mechanism (`CookieDomain`, the shared `DataProtection:SessionKeyPath` key ring).

## License

AGPL-3.0-or-later - see [`LICENSE`](LICENSE). This project also depends on
[JumpStart](https://github.com/cyberknet/JumpStart), a separate GPL-3.0-or-later project - see
[`NOTICE.md`](NOTICE.md) for how the two combine.

## Building standalone

**This repo cannot be built on its own.** It reaches JumpStart via a `ProjectReference` to
`../JumpStart/JumpStart/JumpStart.csproj`, a path that only resolves inside the
[umbrella repo's](https://github.com/RustArchon/RustArchon) submodule layout. Clone that instead:

```bash
git clone --recurse-submodules https://github.com/RustArchon/RustArchon.git
cd RustArchon/RustArchon.Panel
dotnet ef database update   # first run only - see the umbrella README's "Running locally"
dotnet run
```
