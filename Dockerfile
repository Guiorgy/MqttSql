# check=error=true
# syntax=docker/dockerfile:1-labs
# TODO: Remove the above once COPY --parents becomes part of stable syntax

#   This file is part of MqttSql (Copyright © 2024 Guiorgy).
#   MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
#   MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
#   You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.

# Source: https://github.com/dotnet/dotnet-docker/blob/main/README.sdk.md#full-tag-listing
# Ubuntu 24.04: 10.0, 10.0-noble
# Ubuntu 24.04 AOT: 10.0-aot, 10.0-noble-aot
# Alpine: 10.0-alpine
# Alpine AOT: 10.0-alpine-aot
# Azure Linux: 10.0-azurelinux3.0
# Azure Linux AOT: 10.0-azurelinux3.0-aot
ARG SDK_TAG=10.0

# Source: https://github.com/dotnet/dotnet-docker/blob/main/documentation/image-variants.md
# Runtime: runtime
# Runtime with native dependencies: runtime-deps
ARG RUNTIME_IMAGE=runtime

# Source: https://github.com/dotnet/dotnet-docker/blob/main/README.runtime.md#full-tag-listing
# Ubuntu 24.04: 10.0, 10.0-noble
# Ubuntu 24.04 Chiseled: 10.0-noble-chiseled
# Ubuntu 24.04 Chiseled with tzdata (Time Zone Database) and icu (International Components for Unicode): 10.0-noble-chiseled-extra
# Alpine: 10.0-alpine
# Alpine with tzdata (Time Zone Database) and icu (International Components for Unicode): 10.0-alpine-extra
# Azure Linux: 10.0-azurelinux3.0
# Azure Linux Distroless: 10.0-azurelinux3.0-distroless
# Azure Linux Distroless with tzdata (Time Zone Database) and icu (International Components for Unicode): 10.0-azurelinux3.0-distroless-extra
ARG RUNTIME_TAG=10.0

### .NET Build Base Stage ###
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:$SDK_TAG AS build-base
ARG TARGETARCH
ARG BUILD_CONFIGURATION=Release
WORKDIR /build

### .NET Build Stage ###
FROM build-base AS build

# Copy project files only and restore dependencies (as a distinct layers for caching)
COPY --parents *.csproj */*.csproj */*/*.csproj ./
RUN dotnet restore ./MqttSql/MqttSql.csproj --arch $TARGETARCH

# Make the argument available to dotnet build
ARG GIT_COMMIT

# Copy all files (except already copied project files) and build a release executable targeting Linux and the specified architecture
COPY --exclude=*.csproj --exclude=*/*.csproj --exclude=*/*/*.csproj . ./
RUN dotnet build ./MqttSql/MqttSql.csproj \
  --no-restore --nologo \
  --os linux --arch $TARGETARCH --self-contained false --configuration $BUILD_CONFIGURATION \
  -p:UseAppHost=false -p:Define=DOCKER -p:GitRevision="$GIT_COMMIT"

### .NET Debug Base Stage ###
FROM build-base AS debug-base
ARG DEBUG_CONFIG
WORKDIR /app

# Create the configuration file
RUN mkdir /tmp/mqttsql/ && echo -n "$DEBUG_CONFIG" > /tmp/mqttsql/config.json

### .NET App Debug Stage ###
FROM debug-base AS debug

# Copy the built files into the working directory
COPY --from=build /build/MqttSql/bin/Debug/*/*/* .

# Define the entry point
ENTRYPOINT ["dotnet", "MqttSql.dll", "--config=/tmp/mqttsql/config.json", "--logfile=/dev/null", "--sqlite-dir=/tmp/mqttsql/"]

### .NET App Publish Stage ###
FROM build AS publish

# Build a release executable targeting Linux and the specified architecture
RUN dotnet publish ./MqttSql/MqttSql.csproj \
  --no-build --nologo \
  --os linux --arch $TARGETARCH --self-contained false --configuration $BUILD_CONFIGURATION \
  -p:UseAppHost=false \
  --output /publish

# Move the LICENSE file into the output directory
RUN mv ./LICENSE /publish/

### .NET App Runtime Image ###
FROM mcr.microsoft.com/dotnet/$RUNTIME_IMAGE:$RUNTIME_TAG AS runtime
WORKDIR /app

# Copy the published files from the publish stage into the working directory
COPY --from=publish /publish .

# Define the volumes
VOLUME /app/config /app/data

# Define the entry point
ENTRYPOINT ["dotnet", "MqttSql.dll", "--config=/app/config/config.json", "--logfile=/dev/null", "--sqlite-dir=/app/data/"]

ARG TITLE \
  DESCRIPTION \
  VERSION \
  AUTHOR \
  LICENSE \
  SOURCE \
  GIT_COMMIT \
  BUILD_TIMESTAMP \
  RUNTIME_TAG \
  IMAGE_TAG

RUN [ -n "$TITLE" ] \
  && [ -n "$DESCRIPTION" ] \
  && [ -n "$VERSION" ] \
  && [ -n "$AUTHOR" ] \
  && [ -n "$LICENSE" ] \
  && [ -n "$SOURCE" ] \
  && [ -n "$GIT_COMMIT" ] \
  && [ -n "$BUILD_TIMESTAMP" ] \
  && [ -n "$RUNTIME_TAG" ] \
  && [ -n "$IMAGE_TAG" ]

LABEL org.opencontainers.image.title="$TITLE" \
  org.opencontainers.image.description="$DESCRIPTION" \
  org.opencontainers.image.version="$VERSION" \
  org.opencontainers.image.authors="$AUTHOR" \
  org.opencontainers.image.licenses="$LICENSE" \
  org.opencontainers.image.source="$SOURCE" \
  org.opencontainers.image.revision="$GIT_COMMIT" \
  org.opencontainers.image.created="$BUILD_TIMESTAMP" \
  org.opencontainers.image.base.name="docker.io/mcr.microsoft.com/dotnet/runtime:$RUNTIME_TAG" \
  org.opencontainers.image.ref.name="$IMAGE_TAG"
