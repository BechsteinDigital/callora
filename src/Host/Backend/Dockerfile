FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/Host/Backend/Callora.Host.Backend.csproj --nologo
RUN dotnet restore custom/plugins/Voip/Callora.Plugins.Voip.csproj --nologo
RUN dotnet publish custom/plugins/Voip/Callora.Plugins.Voip.csproj -c Release --no-restore --nologo -o /tmp/plugin-publish/Voip
RUN rm -f /tmp/plugin-publish/Voip/VoipHost.PluginContracts.dll /tmp/plugin-publish/Voip/VoipHost.PluginContracts.pdb /tmp/plugin-publish/Voip/Callora.Modules.Abstractions.dll /tmp/plugin-publish/Voip/Callora.Modules.Abstractions.pdb
RUN rm -rf /src/custom/plugins/Voip && mkdir -p /src/custom/plugins/Voip && cp -a /tmp/plugin-publish/Voip/. /src/custom/plugins/Voip/
RUN dotnet publish src/Host/Backend/Callora.Host.Backend.csproj -c Release -o /app/publish --no-restore --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/custom/plugins ./custom/plugins

ENV ASPNETCORE_URLS=http://0.0.0.0:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "Callora.Host.Backend.dll"]
