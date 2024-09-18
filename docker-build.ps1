<#
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
#>

param(
  [string]$Command = 'build',
  [string]$Arch = 'auto',
  [string]$Base = 'default'
)

$TAG = 'guiorgy/mqttsql'

$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

if ($Command -eq 'build') {
  if ($Arch -in 'auto', 'x86', 'x64', 'arm', 'arm64') {
    $ARCH_DOCKER_PLATFORM_MAPPING = @{
      "auto" = "linux"
      "x86" = "linux/386"
      "x64" = "linux/amd64"
      "arm" = "linux/arm/v7"
      "arm64" = "linux/arm64"
    }

    $PLATFORM = $ARCH_DOCKER_PLATFORM_MAPPING[$Arch]
  } else {
    Write-Error "Invalid architecture specified: $Arch. Please use one of: auto, x86, x64, arm, arm64"
  }

  if ($Base -in 'default', 'debian', 'ubuntu', 'ubuntu-chiseled', 'ubuntu-chiseled-extra', 'alpine') {
    $BASE_SDK_IMAGE_TAG_MAPPING = @{
      "default" = "8.0"
      "debian" = "8.0"
      "ubuntu-22" = "8.0-jammy"
      "ubuntu-24" = "8.0-noble"
      "ubuntu" = "8.0-noble"
      "alpine" = "8.0-alpine"
    }

    $BASE_RUNTIME_IMAGE_TAG_MAPPING = @{
      "default" = "8.0"
      "debian" = "8.0"
      "ubuntu-22" = "8.0-jammy"
      "ubuntu-24" = "8.0-noble"
      "ubuntu" = "8.0-noble"
      "ubuntu-chiseled-22" = "8.0-jammy-chiseled"
      "ubuntu-chiseled-24" = "8.0-noble-chiseled"
      "ubuntu-chiseled" = "8.0-noble-chiseled"
      "ubuntu-chiseled-extra-22" = "8.0-jammy-chiseled-extra"
      "ubuntu-chiseled-extra-24" = "8.0-noble-chiseled-extra"
      "ubuntu-chiseled-extra" = "8.0-noble-chiseled-extra"
      "alpine" = "8.0-alpine"
    }

    $SDK_TAG = $BASE_SDK_IMAGE_TAG_MAPPING[$Base]
    $RUNTIME_TAG = $BASE_RUNTIME_IMAGE_TAG_MAPPING[$Base]
  } else {
    Write-Error "Invalid base image specified: $Base. Please use one of: default, debian, ubuntu, ubuntu-chiseled, ubuntu-chiseled-extra, alpine"
  }

  docker build --platform=$PLATFORM --build-arg SDK_TAG=$SDK_TAG --build-arg RUNTIME_TAG=$RUNTIME_TAG --tag "$($TAG):latest" --file MqttSql\Dockerfile .

  Write-Information "Image '$($TAG):latest' built"
} elseif ($Command -eq 'save') {
  New-Item -ItemType Directory -Force -Path .\Publish\Docker > $null
  $ARCHIVE = ".\Publish\Docker\$($TAG -replace '/', '-')-latest.tar.gz"

  if ($PSVersionTable.PSVersion.Major -ge 7) {
    docker save "$($TAG):latest" | gzip --best --stdout --verbose > "$ARCHIVE"
  } else {
    # PowerShell 5 doesn't handle byte streams properly resulting in a corrupted archive
    cmd /c "docker save $($TAG):latest | gzip --best --stdout --verbose > $ARCHIVE"
  }

  Write-Information "Image saved to '$ARCHIVE'"
} else {
  Write-Error "Invalid command specified: $Command. Please use one of: 'build', 'save'"
}
