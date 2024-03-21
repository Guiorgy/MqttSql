param(
  [string]$COMMAND = 'build',
  [string]$ARCH = 'auto'
)

if ($COMMAND -eq 'build') {
  if ($arch -in 'auto', 'x86', 'x64', 'arm', 'arm64') {
    if ($arch -eq 'auto') {
      docker build --platform=linux --tag guiorgy/mqttsql:latest --file Dockerfile .
    } else {
      $ARCH_DOCKER_MAPPING = @{
        "auto" = "auto"
        "x86" = "386"
        "x64" = "amd64"
        "arm" = "arm/v7"
        "arm64" = "arm64"
      }
      $DOCKER_ARCH = $ARCH_DOCKER_MAPPING[$ARCH]

      docker build --platform=linux/$DOCKER_ARCH --tag guiorgy/mqttsql:latest --file Dockerfile .
    }
  } else {
    Write-Error "Invalid architecture specified: $arch. Please use one of: auto, x86, x64, arm, arm64"
  }
} elseif ($COMMAND -eq 'save') {
  New-Item -ItemType Directory -Force -Path .\Publish\Docker
  docker save -o .\Publish\Docker\guiorgy-mqttsql-latest.tar guiorgy/mqttsql:latest
} else {
  Write-Error "Invalid command specified: $command. Please use one of: 'build', 'save'"
}
