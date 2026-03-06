FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY GitNews.sln ./
COPY GitNews.DTO/GitNews.DTO.csproj GitNews.DTO/
COPY GitNews.Domain/GitNews.Domain.csproj GitNews.Domain/
COPY GitNews.Infra.Interfaces/GitNews.Infra.Interfaces.csproj GitNews.Infra.Interfaces/
COPY GitNews.Infra/GitNews.Infra.csproj GitNews.Infra/
COPY GitNews.Application/GitNews.Application.csproj GitNews.Application/
COPY GitNews.Console/GitNews.Console.csproj GitNews.Console/
COPY GitNews.Worker/GitNews.Worker.csproj GitNews.Worker/
RUN dotnet restore GitNews.Worker/GitNews.Worker.csproj

COPY . .
RUN dotnet publish GitNews.Worker/GitNews.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GitNews.Worker.dll"]
