FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY examenParcial.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
# Render define PORT; Program.cs escucha en ese puerto.
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "examenParcial.dll"]
