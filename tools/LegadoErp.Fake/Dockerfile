FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props NuGet.Config ./
COPY tools/LegadoErp.Fake/LegadoErp.Fake.csproj tools/LegadoErp.Fake/
RUN dotnet restore tools/LegadoErp.Fake/LegadoErp.Fake.csproj
COPY tools/LegadoErp.Fake/ tools/LegadoErp.Fake/
RUN dotnet publish tools/LegadoErp.Fake/LegadoErp.Fake.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "LegadoErp.Fake.dll"]
