# Build context is this repo's own root (see docker-compose.yml) - JumpStart is mounted inside this
# repo (as a submodule; see README's "Cloning this repo"), so no wider context is needed the way it
# used to be when JumpStart lived two directories up as a sibling checkout.

# runtime-deps, not aspnet - a self-contained publish (see the publish step below for why) bundles its
# own copy of the .NET runtime, so the final image only needs the OS-level native libraries (libssl,
# libicu, zlib, ...) ASP.NET Core depends on, not a separate installed runtime on top of them.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS base
# Npgsql probes for GSSAPI/Kerberos support on every connection attempt regardless of which auth
# mechanism is actually used (password auth, here) - without this, it's a harmless but noisy
# "Cannot load library libgssapi_krb5.so.2" logged on every single connection open. Confirmed by hand:
# Postgres connectivity works either way, this only silences the warning.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["RustArchon.Panel/RustArchon.Panel.csproj", "RustArchon.Panel/"]
COPY ["RustArchon.Shared/RustArchon.Shared.csproj", "RustArchon.Shared/"]
COPY ["RustArchon.Messaging/RustArchon.Messaging.csproj", "RustArchon.Messaging/"]
COPY ["JumpStart/JumpStart/JumpStart.csproj", "JumpStart/JumpStart/"]
RUN dotnet restore "RustArchon.Panel/RustArchon.Panel.csproj" -r linux-x64

COPY ["RustArchon.Panel/", "RustArchon.Panel/"]
COPY ["RustArchon.Shared/", "RustArchon.Shared/"]
COPY ["RustArchon.Messaging/", "RustArchon.Messaging/"]
COPY ["JumpStart/", "JumpStart/"]
WORKDIR "/src/RustArchon.Panel"

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# Two things had to change here from a typical scaffolded Dockerfile, both confirmed by hand:
#
# 1. --self-contained true -r linux-x64, not the usual framework-dependent publish - a
#    framework-dependent publish of this project silently drops _framework/blazor.web.js (and
#    blazor.server.js) from the output entirely. Every other static web asset publishes fine; just not
#    the two sourced from the ASP.NET Core shared framework itself.
# 2. No --no-restore here, even though `dotnet restore` already ran above - the restore above only
#    ever saw the bare .csproj files (the point of copying just those first is letting Docker cache
#    that expensive layer separately from source changes), and publishing with --no-restore afterward
#    trusts that stub-only restore's cached state instead of reconciling it against the real project
#    now sitting in the build context - which is what was actually causing blazor.web.js to go
#    missing, not self-containment (a red herring this comment used to blame). Skipping restore here
#    is one of the cheaper things this publish does anyway (NuGet packages are already fetched); it's
#    the per-project static-web-assets computation that needs to re-run against the real source, and
#    --no-restore was skipping that too.
RUN dotnet publish "RustArchon.Panel.csproj" -c $BUILD_CONFIGURATION -r linux-x64 --self-contained true -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./RustArchon.Panel"]
