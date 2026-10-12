# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/EthioServe.sln ./backend/
COPY backend/Directory.Build.props ./backend/
COPY backend/EthioServe.API/*.csproj backend/EthioServe.API/
COPY backend/EthioServe.Application/*.csproj backend/EthioServe.Application/
COPY backend/EthioServe.Domain/*.csproj backend/EthioServe.Domain/
COPY backend/EthioServe.Infrastructure/*.csproj backend/EthioServe.Infrastructure/
COPY backend/EthioServe.Tests/*.csproj backend/EthioServe.Tests/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore backend/EthioServe.API/EthioServe.API.csproj

COPY backend ./backend

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish backend/EthioServe.API/EthioServe.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install --yes --no-install-recommends wget \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/App_Data/uploads/requests \
    && chown -R 1654:1654 /app

COPY --from=build --chown=1654:1654 /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER 1654

ENTRYPOINT ["dotnet", "EthioServe.API.dll"]
