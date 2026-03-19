FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props RunCoach.slnx ./
COPY src/RunCoach.Api/RunCoach.Api.csproj src/RunCoach.Api/
COPY tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj tests/RunCoach.Api.Tests/
RUN dotnet restore RunCoach.slnx

COPY . .
RUN dotnet publish src/RunCoach.Api/RunCoach.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
HEALTHCHECK --interval=10s --timeout=5s --retries=3 CMD curl -f http://localhost:5000/health || exit 1
ENTRYPOINT ["dotnet", "RunCoach.Api.dll"]
