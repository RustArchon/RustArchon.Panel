# RustArchon.Panel

The actual RustArchon product: sign-up, login, server registration, the always-on RCON console, and
invitation-code administration - served from `panel.rustarchon.com`. Owns the ASP.NET Core Identity
database; everything else (servers, tenants, roles) is reached through generated API clients calling
[RustArchon.Api](https://github.com/RustArchon/RustArchon.Api). The marketing site
([RustArchon.Web](https://github.com/RustArchon/RustArchon.Web)) is a separate app entirely - this
repo has no marketing pages of its own.

Part of the [RustArchon](https://github.com/RustArchon/RustArchon) system - see that repo for the
full architecture and how to run the whole stack locally or via Docker Compose.

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
```
