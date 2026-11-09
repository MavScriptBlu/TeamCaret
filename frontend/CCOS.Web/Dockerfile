# Builds the CCOS website (Razor Pages) into a container. docker-compose.yml runs it for you (`task up`).
# Build context is the repo root, so this file can reach shared/ and Directory.Build.props.

# --- Stage 1: build with the full .NET SDK ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first and restore packages.
# Docker caches this step, so packages only re-download when a .csproj changes.
COPY Directory.Build.props .editorconfig ./
COPY shared/CCOS.Shared/CCOS.Shared.csproj shared/CCOS.Shared/
COPY frontend/CCOS.Web/CCOS.Web.csproj frontend/CCOS.Web/
RUN dotnet restore frontend/CCOS.Web/CCOS.Web.csproj

# Now copy the actual code and publish a release build
COPY shared/ shared/
COPY frontend/ frontend/
RUN dotnet publish frontend/CCOS.Web/CCOS.Web.csproj -c Release -o /app/publish --no-restore

# --- Stage 2: small runtime-only image (no SDK, way smaller) ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build --chown=$APP_UID /app/publish .
# Run as the built-in non-root "app" user instead of root
USER $APP_UID
# .NET 8+ listens on 8080 inside the container (not 80 like older tutorials)
EXPOSE 8080
ENTRYPOINT ["dotnet", "CCOS.Web.dll"]
