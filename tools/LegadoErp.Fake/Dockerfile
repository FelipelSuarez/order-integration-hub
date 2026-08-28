FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Sem global.json: a imagem já garante um SDK 10.0.x compatível, e o pin de patch exato
# (10.0.301) é só pra determinismo do ambiente de desenvolvimento local, não do container.
COPY Directory.Build.props NuGet.Config ./
COPY tools/LegadoErp.Fake/LegadoErp.Fake.csproj tools/LegadoErp.Fake/
RUN dotnet restore tools/LegadoErp.Fake/LegadoErp.Fake.csproj
COPY tools/LegadoErp.Fake/ tools/LegadoErp.Fake/
RUN dotnet publish tools/LegadoErp.Fake/LegadoErp.Fake.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "LegadoErp.Fake.dll"]
