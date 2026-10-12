#!/bin/sh
set -eu
dotnet /app/AzLocal.Host.dll --create-container-certificate /certs
exec dotnet /app/AzLocal.Host.dll "$@"
