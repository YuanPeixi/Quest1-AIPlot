FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/AIPlot/AIPlot.csproj", "AIPlot/"]
RUN dotnet restore "AIPlot/AIPlot.csproj"
COPY src/AIPlot/ AIPlot/
WORKDIR "/src/AIPlot"
RUN dotnet build "AIPlot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AIPlot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# 数据目录（SQLite 数据库和用户设置）
VOLUME ["/app/data"]
ENV AIPLOT_DB_PATH=/app/data/aiplot.db

ENTRYPOINT ["dotnet", "AIPlot.dll"]
