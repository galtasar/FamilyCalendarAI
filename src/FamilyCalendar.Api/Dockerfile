FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FamilyCalendar.Api/FamilyCalendar.Api.csproj FamilyCalendar.Api/
COPY FamilyCalendar.Core/FamilyCalendar.Core.csproj FamilyCalendar.Core/
COPY FamilyCalendar.Infrastructure/FamilyCalendar.Infrastructure.csproj FamilyCalendar.Infrastructure/
COPY FamilyCalendar.AI/FamilyCalendar.AI.csproj FamilyCalendar.AI/
COPY FamilyCalendar.Email/FamilyCalendar.Email.csproj FamilyCalendar.Email/
COPY FamilyCalendar.Calendar/FamilyCalendar.Calendar.csproj FamilyCalendar.Calendar/
COPY FamilyCalendar.ServiceDefaults/FamilyCalendar.ServiceDefaults.csproj FamilyCalendar.ServiceDefaults/
RUN dotnet restore FamilyCalendar.Api/FamilyCalendar.Api.csproj

COPY . .
RUN dotnet publish FamilyCalendar.Api/FamilyCalendar.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FamilyCalendar.Api.dll"]
