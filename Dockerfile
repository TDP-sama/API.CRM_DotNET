# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY dxpackages/ dxpackages/
COPY API.CRM_DotNET/API.CRM_DotNET.csproj API.CRM_DotNET/
# DevExpress packages come from ./dxpackages, or from your DevExpress NuGet feed passed as the optional
# build secret "devexpress_feed" (https://nuget.devexpress.com/<key>/api/v3/index.json).
RUN --mount=type=secret,id=devexpress_feed \
    if [ -s /run/secrets/devexpress_feed ]; then \
      dotnet nuget add source "$(cat /run/secrets/devexpress_feed)" --name devexpress-feed --configfile nuget.config; \
    fi && \
    dotnet restore API.CRM_DotNET/API.CRM_DotNET.csproj
COPY API.CRM_DotNET/ API.CRM_DotNET/
# Optional build secret "devexpress_license": your DevExpress license key (DevExpress_License.txt content).
RUN --mount=type=secret,id=devexpress_license \
    if [ -s /run/secrets/devexpress_license ]; then export DevExpress_License="$(cat /run/secrets/devexpress_license)"; fi && \
    dotnet publish API.CRM_DotNET/API.CRM_DotNET.csproj -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
# Report rendering on Linux (Skia) needs fontconfig, plus fonts with Vietnamese glyphs.
# Liberation 2.x (fonts-liberation2; the 1.x "fonts-liberation" package lacks Vietnamese) Serif/Sans are
# metric-compatible with Times New Roman/Arial, and fontconfig maps those names to them.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fonts-liberation2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "API.CRM_DotNET.dll"]
