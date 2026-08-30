# Build context is this repo's own root (see docker-compose.yml) - JumpStart is mounted inside this
# repo (as a submodule; see README's "Cloning this repo"), so no wider context is needed the way it
# used to be when JumpStart lived two directories up as a sibling checkout.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["RustArchon.Panel/RustArchon.Panel.csproj", "RustArchon.Panel/"]
COPY ["RustArchon.Shared/RustArchon.Shared.csproj", "RustArchon.Shared/"]
COPY ["JumpStart/JumpStart/JumpStart.csproj", "JumpStart/JumpStart/"]
RUN dotnet restore "RustArchon.Panel/RustArchon.Panel.csproj"

COPY ["RustArchon.Panel/", "RustArchon.Panel/"]
COPY ["RustArchon.Shared/", "RustArchon.Shared/"]
COPY ["JumpStart/", "JumpStart/"]
WORKDIR "/src/RustArchon.Panel"
RUN dotnet build "RustArchon.Panel.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "RustArchon.Panel.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RustArchon.Panel.dll"]
