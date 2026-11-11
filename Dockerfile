# Vê AI · API .NET · imagem de produção em duas etapas (build com o SDK, execução só com o runtime)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY VeAI.sln ./
COPY src/VeAI.Domain/VeAI.Domain.csproj src/VeAI.Domain/
COPY src/VeAI.Application/VeAI.Application.csproj src/VeAI.Application/
COPY src/VeAI.Infrastructure/VeAI.Infrastructure.csproj src/VeAI.Infrastructure/
COPY src/VeAI.Api/VeAI.Api.csproj src/VeAI.Api/
RUN dotnet restore src/VeAI.Api/VeAI.Api.csproj
COPY src/ src/
RUN dotnet publish src/VeAI.Api/VeAI.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "VeAI.Api.dll"]
