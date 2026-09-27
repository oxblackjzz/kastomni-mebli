# Збірка
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/KastomniMebli.Web/KastomniMebli.Web.csproj src/KastomniMebli.Web/
RUN dotnet restore src/KastomniMebli.Web/KastomniMebli.Web.csproj
COPY src/ src/
RUN dotnet publish src/KastomniMebli.Web/KastomniMebli.Web.csproj -c Release -o /app --no-restore

# Запуск
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    LANG=C.UTF-8 \
    LC_ALL=C.UTF-8
COPY --from=build /app .
# Без USER $APP_UID: постійний диск Render монтується з правами root,
# і непривілейований користувач не зміг би писати туди файли CRM.
# Render передає PORT; без нього — стандартний 8080.
EXPOSE 8080
ENTRYPOINT ["dotnet", "KastomniMebli.Web.dll"]
