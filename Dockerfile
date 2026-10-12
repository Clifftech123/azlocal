FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=0.1.0-alpha.0
WORKDIR /src
COPY . .
RUN dotnet restore src/AzLocal.Host -p:MinVerVersionOverride=$VERSION
RUN dotnet publish src/AzLocal.Host -c Release --no-restore -o /app -p:MinVerVersionOverride=$VERSION -p:UseAppHost=false

FROM build AS smoke-build
RUN dotnet restore src/AzLocal.Client -p:MinVerVersionOverride=$VERSION \
    && dotnet pack src/AzLocal.Core -c Release --no-restore -o /packages -p:MinVerVersionOverride=$VERSION \
    && dotnet pack src/AzLocal.Client -c Release --no-restore -o /packages -p:MinVerVersionOverride=$VERSION
RUN mkdir /consumer \
    && cp eng/ContainerSmoke/ContainerSmoke.csproj.template /consumer/ContainerSmoke.csproj \
    && cp eng/PackageSmoke.cs /consumer/Program.cs \
    && sed -i "s/PACKAGE_VERSION/$VERSION/g" /consumer/ContainerSmoke.csproj \
    && dotnet restore /consumer/ContainerSmoke.csproj --source /packages --source https://api.nuget.org/v3/index.json \
    && dotnet publish /consumer/ContainerSmoke.csproj -c Release --no-restore -o /smoke -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS smoke
WORKDIR /app
COPY --from=smoke-build /smoke .
USER $APP_UID
ENTRYPOINT ["dotnet", "ContainerSmoke.dll", "https://127.0.0.1:4566", "5672", "/certs/localhost.crt"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG VERSION=0.1.0-alpha.0
LABEL org.opencontainers.image.title="azlocal" \
      org.opencontainers.image.description="Local Azure emulator for development and testing" \
      org.opencontainers.image.source="https://github.com/Clifftech123/azlocal" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.version=$VERSION
WORKDIR /app
COPY --from=build /app .
COPY eng/docker-entrypoint.sh /app/docker-entrypoint.sh
RUN mkdir -p /data/blobs /certs && chown -R $APP_UID:$APP_UID /data /certs && chmod +x /app/docker-entrypoint.sh
ENV ASPNETCORE_URLS=https://0.0.0.0:4566 \
    AzLocal__ContainerNetworking=true \
    AzLocal__StateMode=Sqlite \
    AzLocal__SqlitePath=/data \
    AzLocal__SnapshotPath=/data \
    AzLocal__BlobStorePath=/data/blobs \
    Kestrel__Certificates__Default__Path=/certs/localhost.pfx
EXPOSE 4566 5672
USER $APP_UID
HEALTHCHECK --interval=5s --timeout=4s --start-period=10s --retries=10 CMD ["dotnet", "AzLocal.Host.dll", "--healthcheck"]
ENTRYPOINT ["/app/docker-entrypoint.sh"]
