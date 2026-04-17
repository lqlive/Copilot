# See https://aka.ms/customizecontainer to learn how to customize your debug container
# and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Base runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER root
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        bash \
        ca-certificates \
        curl \
        git \
    && curl -fsSL https://gh.io/copilot-install | bash \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Copilot.Agent/Copilot.Agent.csproj", "src/Copilot.Agent/"]
COPY ["src/Copilot.Provider.GitLab/Copilot.Provider.GitLab.csproj", "src/Copilot.Provider.GitLab/"]
COPY ["src/Copilot.Core/Copilot.Core.csproj", "src/Copilot.Core/"]

RUN dotnet restore "./src/Copilot.Agent/Copilot.Agent.csproj"

COPY . .
WORKDIR "/src/src/Copilot.Agent"
RUN dotnet build "./Copilot.Agent.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Copilot.Agent.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Copilot.Agent.dll"]