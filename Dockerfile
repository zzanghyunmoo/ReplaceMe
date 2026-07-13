FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY docker/certs/ /usr/local/share/ca-certificates/devautomation-extra/
RUN set -eu; certs="$(find /usr/local/share/ca-certificates/devautomation-extra -name '*.crt' -print -quit)"; if [ -n "$certs" ]; then update-ca-certificates; fi
COPY . .
RUN dotnet restore src/DevAutomation.Api/DevAutomation.Api.csproj \
    && dotnet publish src/DevAutomation.Api/DevAutomation.Api.csproj -c Release -o /out/api --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api
WORKDIR /app
COPY --from=build /out/api ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DevAutomation.Api.dll"]
