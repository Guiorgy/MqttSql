# syntax=docker/dockerfile:1.7-labs
# TODO: Remove the above once COPY --parents becomes part of stable syntax

### .NET App Build Image ###
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
ARG BUILD_CONFIGURATION=Release
WORKDIR /build

# Copy project files only and restore dependencies (as a distinct layers for caching)
COPY --parents *.csproj */*.csproj */*/*.csproj ./
RUN dotnet restore ./MqttSql/MqttSql.csproj --arch $TARGETARCH

# Copy all files (except already copied project files) and build a release executable targeting Linux and the specified architecture
COPY --exclude=*.csproj --exclude=*/*.csproj --exclude=*/*/*.csproj . ./
RUN dotnet build ./MqttSql/MqttSql.csproj --no-restore --nologo --os linux --arch $TARGETARCH --self-contained false --configuration $BUILD_CONFIGURATION -p:UseAppHost=false -p:Define=DOCKER

### .NET App Publish Image ###
FROM build as publish
ARG BUILD_CONFIGURATION=Release

# Build a release executable targeting Linux and the specified architecture
RUN dotnet publish ./MqttSql/MqttSql.csproj --no-build --nologo --os linux --arch $TARGETARCH --self-contained false --configuration $BUILD_CONFIGURATION -p:UseAppHost=false --output /publish

# Move the LICENSE file into the output directory
RUN mv ./LICENSE /publish/

### .NET App Runtime Image ###
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/runtime:8.0 as runtime

# Copy the published files from the publish stage into the working directory
WORKDIR /app
COPY --from=publish /publish .

# Set environmental variables
ENV MqttSqlHome=/app/home

# Create a new user (without home) and run the app with that user instead of root (default)
RUN groupadd mqttsql --gid=10101 \
  && useradd --no-user-group --gid mqttsql --uid=10101 --no-log-init --shell=/bin/bash -M mqttsql
USER mqttsql:mqttsql

# Define the entry point
ENTRYPOINT ["dotnet", "MqttSql.dll"]
