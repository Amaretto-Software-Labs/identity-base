# syntax=docker/dockerfile:1.7

# --------------------------------------
# Build stage
# --------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# copy project files and restore
COPY Identity.sln ./
COPY Identity.Base/Identity.Base.csproj Identity.Base/
COPY Identity.Base.Tests/Identity.Base.Tests.csproj Identity.Base.Tests/
RUN dotnet restore Identity.Base/Identity.Base.csproj

# copy the remaining source and publish
COPY . ./
RUN dotnet publish Identity.Base/Identity.Base.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# --------------------------------------
# Runtime stage
# --------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# create non-root user
RUN groupadd --gid 2000 app && \
    useradd  --uid 2001 --gid app --home /app --create-home app

WORKDIR /app

# copy published output
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080
USER app

ENTRYPOINT ["dotnet", "Identity.Base.dll"]
