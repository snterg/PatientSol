# Базовый образ для запуска приложения
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Образ для сборки приложения
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Копируем файл проекта и восстанавливаем зависимости
COPY ["PatientAPI.csproj", "."]
RUN dotnet restore "PatientAPI.csproj"
# Копируем все остальные файлы
COPY . .
# Собираем приложение
RUN dotnet build "PatientAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Образ для публикации
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "PatientAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Финальный образ
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Точка входа - запуск приложения
ENTRYPOINT ["dotnet", "PatientAPI.dll"]