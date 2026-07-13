FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY docker/certs/ /usr/local/share/ca-certificates/devautomation-extra/
RUN set -eu; certs="$(find /usr/local/share/ca-certificates/devautomation-extra -name '*.crt' -print -quit)"; if [ -n "$certs" ]; then update-ca-certificates; fi
COPY . .
RUN dotnet restore DevAutomation.sln \
    && dotnet publish src/DevAutomation.Api/DevAutomation.Api.csproj -c Release -o /out/api --no-restore \
    && dotnet publish src/DevAutomation.Worker/DevAutomation.Worker.csproj -c Release -o /out/worker --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS worker
WORKDIR /app
COPY --from=build /out/worker ./
ENTRYPOINT ["dotnet", "DevAutomation.Worker.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api
# hadolint ignore=DL3008
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out/api ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DevAutomation.Api.dll"]
