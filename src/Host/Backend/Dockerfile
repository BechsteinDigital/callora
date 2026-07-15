FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/Host/Backend/Callora.Host.Backend.csproj --nologo
RUN dotnet restore custom/static-plugins/Communication/Callora.Plugin.Communication.csproj --nologo
RUN dotnet publish custom/static-plugins/Communication/Callora.Plugin.Communication.csproj -c Release --no-restore --nologo -o /tmp/plugin-publish/Communication
RUN rm -f /tmp/plugin-publish/Communication/VoipHost.PluginContracts.dll /tmp/plugin-publish/Communication/VoipHost.PluginContracts.pdb /tmp/plugin-publish/Communication/Callora.Modules.Abstractions.dll /tmp/plugin-publish/Communication/Callora.Modules.Abstractions.pdb
RUN rm -rf /src/custom/static-plugins/Communication && mkdir -p /src/custom/static-plugins/Communication && cp -a /tmp/plugin-publish/Communication/. /src/custom/static-plugins/Communication/
RUN dotnet publish src/Host/Backend/Callora.Host.Backend.csproj -c Release -o /app/publish --no-restore --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/custom/plugins ./custom/plugins
COPY --from=build /src/custom/static-plugins ./custom/static-plugins

ENV ASPNETCORE_URLS=http://0.0.0.0:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "Callora.Host.Backend.dll"]
