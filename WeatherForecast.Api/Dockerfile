FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG TARGETARCH
WORKDIR /source

COPY --link WeatherForecast.Api/*.csproj .
RUN dotnet restore -a $TARGETARCH

COPY --link WeatherForecast.Api/. .
RUN dotnet publish -a $TARGETARCH --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
EXPOSE 8080
WORKDIR /app
COPY --link --from=build /app .
ENTRYPOINT ["./WeatherForecast.Api"]