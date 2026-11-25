FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/InternshipCoach.Api/InternshipCoach.Api.csproj src/InternshipCoach.Api/
COPY src/InternshipCoach.Application/InternshipCoach.Application.csproj src/InternshipCoach.Application/
COPY src/InternshipCoach.Contracts/InternshipCoach.Contracts.csproj src/InternshipCoach.Contracts/
COPY src/InternshipCoach.Domain/InternshipCoach.Domain.csproj src/InternshipCoach.Domain/
COPY src/InternshipCoach.Infrastructure/InternshipCoach.Infrastructure.csproj src/InternshipCoach.Infrastructure/
RUN dotnet restore src/InternshipCoach.Api/InternshipCoach.Api.csproj
COPY src/ src/
RUN dotnet publish src/InternshipCoach.Api/InternshipCoach.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "InternshipCoach.Api.dll"]
