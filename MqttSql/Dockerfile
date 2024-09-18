# syntax=docker/dockerfile:1.7-labs
# TODO: Remove the above once COPY --parents becomes part of stable syntax

#   This file is part of MqttSql (Copyright © 2024 Guiorgy).
#   MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
#   MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
#   You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.

# Debian Slim: 8.0
# Ubuntu 22.04: 8.0-jammy
# Ubuntu 24.04: 8.0-noble
# Alpine: 8.0-alpine
ARG SDK_TAG=8.0

# Debian Slim: 8.0
# Ubuntu 22.04: 8.0-jammy
# Ubuntu 24.04: 8.0-noble
# Ubuntu 22.04 Chiseled: 8.0-jammy-chiseled
# Ubuntu 24.04 Chiseled: 8.0-noble-chiseled
# Ubuntu 22.04 Chiseled with tzdata (Time Zone Database) and icu (International Components for Unicode): 8.0-jammy-chiseled-extra
# Ubuntu 24.04 Chiseled with tzdata (Time Zone Database) and icu (International Components for Unicode): 8.0-noble-chiseled-extra
# Alpine: 8.0-alpine
ARG RUNTIME_TAG=8.0

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

# Copy all files (except already copied project files) and build a release executable targeting Linux and the specified architecture
COPY --exclude=*.csproj --exclude=*/*.csproj --exclude=*/*/*.csproj . ./
RUN dotnet build ./MqttSql/MqttSql.csproj --no-restore --nologo --os linux --arch $TARGETARCH --self-contained false --configuration $BUILD_CONFIGURATION -p:UseAppHost=false -p:Define=DOCKER

### .NET Debug Base Stage ###
FROM build-base as debug-base
ARG DEBUG_CONFIG
WORKDIR /app

# Set environmental variables
ENV MqttSqlHome=/tmp/mqttsql

# Create the configuration file
RUN mkdir /tmp/mqttsql/ && echo -n "$DEBUG_CONFIG" > /tmp/mqttsql/config.json

### .NET App Debug Stage ###
FROM debug-base as debug

# Copy the built files into the working directory
COPY --from=build /build/MqttSql/bin/Debug/*/*/* .

# Define the entry point
ENTRYPOINT ["dotnet", "MqttSql.dll"]

### .NET App Publish Stage ###
FROM build as publish

# Build a release executable targeting Linux and the specified architecture
RUN dotnet publish ./MqttSql/MqttSql.csproj --no-build --nologo --os linux --arch $TARGETARCH --self-contained false --configuration $BUILD_CONFIGURATION -p:UseAppHost=false --output /publish

# Move the LICENSE file into the output directory
RUN mv ./LICENSE /publish/

### .NET App Runtime Image ###
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/runtime:$RUNTIME_TAG as runtime
WORKDIR /app

# Copy the published files from the publish stage into the working directory
COPY --from=publish /publish .

# Set environmental variables
ENV MqttSqlHome=/app/home

# Define the entry point
ENTRYPOINT ["dotnet", "MqttSql.dll"]
