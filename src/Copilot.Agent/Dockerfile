# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends git nodejs npm ca-certificates \
    && npm install -g @github/copilot \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
USER $APP_UID


# This stage is used to build the service project
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

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Copilot.Agent.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Copilot.Agent.dll"]