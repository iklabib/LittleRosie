#!/bin/bash
cd "$(dirname "$0")"
dotnet publish -r linux-x64 -o output LittleRosie.csproj
rm -rf bin/ obj/
