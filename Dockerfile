FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
ARG SERVICE
WORKDIR /src
COPY Directory.Build.props .
COPY src/ src/
RUN dotnet publish "src/Services/HealthSynki.${SERVICE}/HealthSynki.${SERVICE}.EntryPoint/HealthSynki.${SERVICE}.EntryPoint.csproj" -c Release -o /out -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:11.0
ARG SERVICE
ENV SERVICE=$SERVICE ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["/bin/sh","-c","exec dotnet HealthSynki.${SERVICE}.EntryPoint.dll"]
