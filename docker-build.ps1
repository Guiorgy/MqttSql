param(
  [string]$Command = 'build',
  [string]$Arch = 'auto',
  [string]$Base = 'default'
)

$TAG = 'guiorgy/mqttsql'

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
      "ubuntu" = "8.0-jammy"
      "ubuntu-chiseled" = "8.0-jammy"
      "ubuntu-chiseled-extra" = "8.0-jammy"
      "alpine" = "8.0-alpine"
    }

    $BASE_RUNTIME_IMAGE_TAG_MAPPING = @{
      "default" = "8.0"
      "debian" = "8.0"
      "ubuntu" = "8.0-jammy"
      "ubuntu-chiseled" = "8.0-jammy-chiseled"
      "ubuntu-chiseled-extra" = "8.0-jammy-chiseled-extra"
      "alpine" = "8.0-alpine"
    }

    $SDK_TAG = $BASE_SDK_IMAGE_TAG_MAPPING[$Base]
    $RUNTIME_TAG = $BASE_RUNTIME_IMAGE_TAG_MAPPING[$Base]
  } else {
    Write-Error "Invalid base image specified: $Base. Please use one of: default, debian, ubuntu, ubuntu-chiseled, ubuntu-chiseled-extra, alpine"
  }

  docker build --platform=$PLATFORM --build-arg SDK_TAG=$SDK_TAG --build-arg RUNTIME_TAG=$RUNTIME_TAG --tag "$($TAG):latest" --file MqttSql\Dockerfile .
} elseif ($Command -eq 'save') {
  New-Item -ItemType Directory -Force -Path .\Publish\Docker
  docker save "$($TAG):latest" | gzip --best > .\Publish\Docker\$($TAG -replace '/', '-')-latest.tar.gz
} else {
  Write-Error "Invalid command specified: $Command. Please use one of: 'build', 'save'"
}
