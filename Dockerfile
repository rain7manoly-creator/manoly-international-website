# ── Stage 1: Build Tailwind CSS ────────────────────────────────────────────
FROM node:20-alpine AS css-builder
WORKDIR /src
COPY package*.json ./
RUN npm ci
# Tailwind scans Views + wwwroot/js to purge unused classes
COPY tailwind.config.js ./
COPY wwwroot/css/input.css ./wwwroot/css/input.css
COPY wwwroot/js/ ./wwwroot/js/
COPY Views/ ./Views/
RUN npm run css:build

# ── Stage 2: Build & Publish .NET App ──────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies first (layer-cached unless .csproj changes)
COPY ManolyWarehouse.csproj ./
RUN dotnet restore

# Copy the rest of the source
COPY . .

# Overwrite site.css with the freshly compiled version from Stage 1
COPY --from=css-builder /src/wwwroot/css/site.css ./wwwroot/css/site.css

RUN dotnet publish ManolyWarehouse.csproj -c Release -o /app/publish --no-restore

# ── Stage 3: Runtime Image ─────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Container listens on port 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["dotnet", "ManolyWarehouse.dll"]
