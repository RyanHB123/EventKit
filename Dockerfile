# Build from the repository root. The API serves the compiled React frontend,
# so the complete app runs as one Render web service.
ARG DOTNET_VERSION=10.0

FROM node:24-bookworm-slim AS frontend
WORKDIR /src
COPY frontend/package.json frontend/package-lock.json ./frontend/
RUN npm ci --prefix frontend
COPY frontend/ ./frontend/
RUN npm run build --prefix frontend

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY NuGet.Config ./
COPY backend/EventKit.Api/EventKit.Api.csproj ./backend/EventKit.Api/
RUN dotnet restore backend/EventKit.Api/EventKit.Api.csproj
COPY backend/ ./backend/
RUN dotnet publish backend/EventKit.Api/EventKit.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    --property:UseAppHost=false
COPY --from=frontend /src/frontend/dist/ /app/publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
# Render terminates HTTPS at its reverse proxy and forwards the original scheme.
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
COPY --from=build /app/publish ./
USER $APP_UID
EXPOSE 10000

# Render supplies PORT. exec forwards shutdown signals to the .NET process.
CMD ["sh", "-c", "exec dotnet EventKit.Api.dll --urls \"http://0.0.0.0:${PORT:-10000}\""]
