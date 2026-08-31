FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/AiGateway_Proxy/AiGateway_Proxy.csproj", "src/AiGateway_Proxy/"]
COPY ["src/AiGateway_Core/AiGateway_Core.csproj", "src/AiGateway_Core/"]
COPY ["src/AiGateway_Infrastructure/AiGateway_Infrastructure.csproj", "src/AiGateway_Infrastructure/"]


RUN dotnet restore "src/AiGateway_Proxy/AiGateway_Proxy.csproj"

COPY . .
WORKDIR "/src/src/AiGateway_Proxy"
RUN dotnet build "AiGateway_Proxy.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AiGateway_Proxy.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AiGateway_Proxy.dll"]