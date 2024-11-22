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

if ($Command -eq 'build') {
  $ARCH_DOCKER_PLATFORM_MAPPING = @{
    'linux' = @('auto')
    'linux/386' = @('x86')
    'linux/amd64' = @('x64')
    'linux/arm/v7' = @('arm')
    'linux/arm64' = @('arm64')
  }

  $PLATFORM = Get-ValueFromMapping -Mapping $ARCH_DOCKER_PLATFORM_MAPPING -Key $Arch

  if (-not $PLATFORM) {
    Write-Error "Invalid architecture specified: $Arch. Please use one of: $($(Get-AllKeysFromMapping -Mapping $ARCH_DOCKER_PLATFORM_MAPPING) -join ', ')"
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

  $SDK_TAG = Get-ValueFromMapping -Mapping $BASE_SDK_IMAGE_TAG_MAPPING -Key $Base
  $RUNTIME_TAG = Get-ValueFromMapping -Mapping $BASE_RUNTIME_IMAGE_TAG_MAPPING -Key $Base

  if (-not $SDK_TAG -or -not $RUNTIME_TAG) {
    Write-Error "Invalid base image specified: $Base. Please use one of: $($(Get-AllKeysFromMapping -Mapping $BASE_RUNTIME_IMAGE_TAG_MAPPING) -join ', ')"
  }

  Write-Information "Building image '$($TAG):latest'"

  docker build --platform=$PLATFORM --build-arg SDK_TAG=$SDK_TAG --build-arg RUNTIME_TAG=$RUNTIME_TAG --tag "$($TAG):latest" --file MqttSql\Dockerfile .

  Write-Information "Image '$($TAG):latest' built"
} elseif ($Command -eq 'save') {
  New-Item -ItemType Directory -Force -Path .\Publish\Docker > $null
  $ARCHIVE = ".\Publish\Docker\$($TAG -replace '/', '-')-latest.tar.gz"

  Write-Information "Saving image to '$ARCHIVE'"

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
