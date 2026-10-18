FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY API.CRM_DotNET/API.CRM_DotNET.csproj API.CRM_DotNET/
RUN dotnet restore API.CRM_DotNET/API.CRM_DotNET.csproj
COPY API.CRM_DotNET/ API.CRM_DotNET/
RUN dotnet publish API.CRM_DotNET/API.CRM_DotNET.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "API.CRM_DotNET.dll"]
