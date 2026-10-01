FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT=OrderBridge.Api
WORKDIR /source
COPY Directory.Build.props ./
COPY src ./src
RUN dotnet restore src/${PROJECT}/${PROJECT}.csproj --locked-mode
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj --no-restore -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG PROJECT=OrderBridge.Api
ENV APP_DLL=${PROJECT}.dll
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
ENTRYPOINT ["sh","-c","exec dotnet \"$APP_DLL\""]
