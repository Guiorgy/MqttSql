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

$IMAGE_TAG = 'guiorgy/mqttsql'

$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

function Get-ValueFromMapping {
  param (
    [Parameter(Mandatory)]
    [hashtable]$Mapping,

    [Parameter(Mandatory)]
    [string]$Key
  )

  foreach ($value in $Mapping.Keys) {
    if ($Mapping[$value] -contains $Key) {
      return $value
    }
  }

  return $null
}

function Get-AllKeysFromMapping {
  param (
    [Parameter(Mandatory)]
    [hashtable]$Mapping
  )

  $keys = @()
  foreach ($value in $Mapping.Keys) {
    $keys += $Mapping[$value]
  }

  return $keys | Sort-Object
}

$ARCH_DOCKER_PLATFORM_MAPPING = @{
  'linux' = @('auto')
  'linux/386' = @('x86')
  'linux/amd64' = @('x64')
  'linux/arm/v7' = @('arm')
  'linux/arm64' = @('arm64')
}

$BASE_SDK_IMAGE_TAG_MAPPING = @{
  '9.0' = @('default', 'debian')
  '9.0-noble' = @('ubuntu', 'ubuntu-chiseled', 'ubuntu-chiseled-extra', 'ubuntu-24', 'ubuntu-24-chiseled', 'ubuntu-24-chiseled-extra')
  '9.0-alpine' = @('alpine')
}

$BASE_RUNTIME_IMAGE_TAG_MAPPING = @{
  '9.0' = @('default', 'debian')
  '9.0-noble' = @('ubuntu', 'ubuntu-24')
  '9.0-noble-chiseled' = @('ubuntu-chiseled', 'ubuntu-24-chiseled')
  '9.0-noble-chiseled-extra' = @('ubuntu-chiseled-extra', 'ubuntu-24-chiseled-extra')
  '9.0-alpine' = @('alpine')
}

if ($Command -eq 'build') {
  $PLATFORM = Get-ValueFromMapping -Mapping $ARCH_DOCKER_PLATFORM_MAPPING -Key $Arch

  if (-not $PLATFORM) {
    Write-Error "Invalid architecture specified: $Arch. Please use one of: $($(Get-AllKeysFromMapping -Mapping $ARCH_DOCKER_PLATFORM_MAPPING) -join ', ')"
  }

  $SDK_IMAGE_TAG = Get-ValueFromMapping -Mapping $BASE_SDK_IMAGE_TAG_MAPPING -Key $Base
  $RUNTIME_IMAGE_TAG = Get-ValueFromMapping -Mapping $BASE_RUNTIME_IMAGE_TAG_MAPPING -Key $Base

  if (-not $SDK_IMAGE_TAG -or -not $RUNTIME_IMAGE_TAG) {
    Write-Error "Invalid base image specified: $Base. Please use one of: $($(Get-AllKeysFromMapping -Mapping $BASE_RUNTIME_IMAGE_TAG_MAPPING) -join ', ')"
  }

  Write-Information "Building image '$($IMAGE_TAG):latest'"

  docker build `
    --platform=$PLATFORM `
    --build-arg SDK_TAG=$SDK_IMAGE_TAG `
    --build-arg RUNTIME_TAG=$RUNTIME_IMAGE_TAG `
    --build-arg TITLE="MqttSql Treon" `
    --build-arg DESCRIPTION="Service that subscribes to MQTT brokers and writes the messages to local SQLite databases" `
    --build-arg VERSION="$(([Xml](Get-Content MqttSql\MqttSql.csproj)).Project.PropertyGroup.Version)" `
    --build-arg AUTHOR=Guiorgy `
    --build-arg LICENSE="GNU Affero General Public License v3.0" `
    --build-arg SOURCE="github.com/Guiorgy/MqttSql" `
    --build-arg GIT_COMMIT=$(git rev-parse HEAD) `
    --build-arg BUILD_TIMESTAMP=$(Get-Date -Format 'yyyy-MM-ddTHH:mm:sszzz') `
    --build-arg IMAGE_TAG="$($IMAGE_TAG):latest" `
    --tag "$($IMAGE_TAG):latest" `
    --file MqttSql\Dockerfile `
    .

  if ($?) {
    Write-Information "Image '$($IMAGE_TAG):latest' built"
  } else {
    Write-Error "Image '$($IMAGE_TAG):latest' build failed"
  }
} elseif ($Command -eq 'save') {
  New-Item -ItemType Directory -Force -Path .\Publish\Docker > $null
  $ARCHIVE = ".\Publish\Docker\$($IMAGE_TAG -replace '/', '-')-latest.tar.gz"

  Write-Information "Saving image to '$ARCHIVE'"

  if ($PSVersionTable.PSVersion.Major -ge 7) {
    docker save "$($IMAGE_TAG):latest" | gzip --best --stdout --verbose > "$ARCHIVE"
  } else {
    # PowerShell 5 doesn't handle byte streams properly resulting in a corrupted archive
    cmd /c "docker save $($IMAGE_TAG):latest | gzip --best --stdout --verbose > $ARCHIVE"
  }

  Write-Information "Image saved to '$ARCHIVE'"
} elseif ($Command -eq 'help') {
  Write-Host "Description:" -ForegroundColor Yellow
  Write-Host "  This script builds, saves, and manages Docker images for $($IMAGE_TAG).`n" -ForegroundColor Green

  $SCRIPT_NAME = $MyInvocation.MyCommand.Name
  Write-Host "Usage:" -ForegroundColor Yellow
  Write-Host "  .\$SCRIPT_NAME -Command <command> -Arch <architecture> -Base <base tag>" -ForegroundColor Green
  Write-Host "  .\$SCRIPT_NAME <command>" -ForegroundColor Green
  Write-Host "  .\$SCRIPT_NAME <command> <architecture>" -ForegroundColor Green
  Write-Host "  .\$SCRIPT_NAME <command> <architecture> <base tag>`n" -ForegroundColor Green

  Write-Host "Note:" -ForegroundColor Yellow
  Write-Host "  When executing this script from cmd.exe, arguments are not passed correctly, so the default arguments will be used (highlighted with square brackets below []).`n" -ForegroundColor Red

  Write-Host "Commands:" -ForegroundColor Yellow
  Write-Host "  [build]  - Build the Docker image." -ForegroundColor Green
  Write-Host "  save     - Save the Docker image to an archive." -ForegroundColor Green
  Write-Host "  help     - Display this help page.`n" -ForegroundColor Green

  Write-Host "Options:" -ForegroundColor Yellow
  Write-Host "  -Arch: Architecture of the Docker image to build." -ForegroundColor Green
  $ARCH_OPTION_VALUES = (Get-AllKeysFromMapping -Mapping $ARCH_DOCKER_PLATFORM_MAPPING) | ForEach-Object { if ($_ -eq 'auto') { "[$_]" } else { "$_" } }
  Write-Host "    $($ARCH_OPTION_VALUES -join ', ')`n" -ForegroundColor Green

  Write-Host "  -Base: Base image tag for the Docker image." -ForegroundColor Green
  $DEFAULT_IMAGE_TAG = Get-ValueFromMapping -Mapping $BASE_SDK_IMAGE_TAG_MAPPING -Key "default"
  $BASE_OPTION_VALUES = @()
  foreach ($TAG in $BASE_RUNTIME_IMAGE_TAG_MAPPING.Keys) {
    foreach ($BASE in $BASE_RUNTIME_IMAGE_TAG_MAPPING[$TAG]) {
      if ($TAG -eq "$DEFAULT_IMAGE_TAG") {
        if ($BASE -ne 'default') {
          $BASE_OPTION_VALUES += "[$BASE]"
        }
      } else {
        $BASE_OPTION_VALUES += $BASE
      }
    }
  }
  Write-Host "    $($BASE_OPTION_VALUES -join ', ')`n" -ForegroundColor Green
} else {
  Write-Error "Invalid command specified: $Command. Please use one of: 'build', 'save', 'help'"
}
