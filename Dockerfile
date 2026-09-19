# syntax=docker/dockerfile:1

# Container image for the YuE to Logic web app (API + Vue frontend), published to ghcr.io by CI.
#   docker build -t yue-to-logic .
#   docker run --rm -p 8080:8080 yue-to-logic      → http://localhost:8080

# ---- Vue frontend ----------------------------------------------------------------------------------
# Build stages run on the builder's platform; their output (JavaScript, portable .NET IL) runs anywhere.
FROM --platform=$BUILDPLATFORM node:24-alpine AS client
WORKDIR /src
COPY src/YueToLogic.Api/ClientApp/package.json src/YueToLogic.Api/ClientApp/package-lock.json ./
RUN npm ci
COPY src/YueToLogic.Api/ClientApp/ ./
RUN npm run build

# ---- ASP.NET Core API ------------------------------------------------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Project files first, so the NuGet restore layer is reused as long as dependencies do not change.
COPY src/YueToLogic.Core/YueToLogic.Core.csproj src/YueToLogic.Core/
COPY src/YueToLogic.Api/YueToLogic.Api.csproj src/YueToLogic.Api/
RUN dotnet restore src/YueToLogic.Api/YueToLogic.Api.csproj
COPY src/YueToLogic.Core/ src/YueToLogic.Core/
COPY src/YueToLogic.Api/ src/YueToLogic.Api/
# The frontend comes from the client stage, so the publish target must not run npm here.
RUN dotnet publish src/YueToLogic.Api/YueToLogic.Api.csproj \
      --configuration Release --no-restore --output /app \
      -p:SkipClientAppBuild=true -p:UseAppHost=false

# ---- Runtime ---------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app ./
COPY --from=client /src/dist ./wwwroot/ui

# The base image listens on 8080 (ASPNETCORE_HTTP_PORTS) and provides the unprivileged user "app".
ENV DOTNET_NOLOGO=true
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "YueToLogic.Api.dll"]
