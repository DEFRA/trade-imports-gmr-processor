# Base dotnet image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Add curl to template.
# CDP PLATFORM HEALTHCHECK REQUIREMENT
RUN apt update && \
    apt install curl -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Build stage image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore tools
ARG DEFRA_NUGET_PAT
ENV DEFRA_NUGET_PAT=${DEFRA_NUGET_PAT}

COPY .config/dotnet-tools.json .config/
COPY .csharpierrc .csharpierrc
RUN dotnet tool restore

# Copy solution and project files for restore
COPY GmrProcessor.slnx .
COPY src/GmrProcessor/*.csproj src/GmrProcessor/
COPY tests/GmrProcessor.Tests/*.csproj tests/GmrProcessor.Tests/
COPY tests/GmrProcessor.IntegrationTests/*.csproj tests/GmrProcessor.IntegrationTests/
COPY tests/TestFixtures/TestFixtures.csproj tests/TestFixtures/TestFixtures.csproj

COPY Directory.Build.props Directory.Build.props
COPY NuGet.config NuGet.config


RUN dotnet restore

# Copy source code
COPY src/ src/
COPY tests/ tests/

# Check code formatting
RUN dotnet csharpier check .

# unit test and code coverage (exclude integration tests)
RUN dotnet test --filter "Category!=Integration"

FROM build AS publish
RUN dotnet publish src/GmrProcessor -c Release -o /app/publish /p:UseAppHost=false


ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

# Final production image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8085
ENTRYPOINT ["dotnet", "GmrProcessor.dll"]
