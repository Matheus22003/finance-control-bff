FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /source

COPY FinanceControl.Bff.sln ./
COPY src/FinanceControl.Bff/FinanceControl.Bff.csproj src/FinanceControl.Bff/
COPY src/FinanceControl.Bff/packages.lock.json src/FinanceControl.Bff/
RUN dotnet restore src/FinanceControl.Bff/FinanceControl.Bff.csproj --locked-mode

COPY src/FinanceControl.Bff/ src/FinanceControl.Bff/
RUN dotnet publish src/FinanceControl.Bff/FinanceControl.Bff.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER $APP_UID
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["dotnet", "FinanceControl.Bff.dll"]
