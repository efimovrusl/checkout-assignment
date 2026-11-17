# syntax=docker/dockerfile:1
ARG NAME=PaymentGateway
ARG CONFIGURATION=Release

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG NAME
ARG CONFIGURATION
WORKDIR /src
COPY nuget.config ./
COPY --parents src/*/*.csproj ./
RUN dotnet restore src/${NAME}.Api
COPY src/ src/
RUN dotnet publish src/${NAME}.Api -c ${CONFIGURATION} -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PaymentGateway.Api.dll"]
