FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build
COPY LittleRosie.csproj LittleRosie.csproj
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages dotnet restore
COPY runtimeconfig.template.json .
COPY . .
RUN dotnet publish -r linux-x64 -o output LittleRosie.csproj

FROM docker.io/library/ubuntu:noble
COPY --from=build /build/output /app
ENTRYPOINT ["/app/LittleRosie"]
