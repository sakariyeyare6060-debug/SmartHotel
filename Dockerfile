FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY SmartHotel/SmartHotel.csproj SmartHotel/
RUN dotnet restore SmartHotel/SmartHotel.csproj
COPY SmartHotel/ SmartHotel/
RUN dotnet publish SmartHotel/SmartHotel.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartHotel.dll"]
