# Версії зафіксовані — ті самі, з якими проєкт зібрано й перевірено локально.
# Оновлювати разом: SDK 10.0.x і рантайм 10.0.x.

# Збірка
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY src/ src/
RUN dotnet --version \
 && dotnet publish src/KastomniMebli.Web/KastomniMebli.Web.csproj -c Release -o /app \
 # Без скрипта Blazor сторінки CRM порожні — краще впасти на збірці, ніж задеплоїти зламане.
 && test -f /app/wwwroot/_framework/blazor.web.js \
 || (echo "ПОМИЛКА: у збірці немає wwwroot/_framework/blazor.web.js" && ls -R /app/wwwroot | head -50 && exit 1)

# Запуск
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12
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
