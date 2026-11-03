FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BookstoreApi/BookstoreApi.csproj src/BookstoreApi/
RUN dotnet restore src/BookstoreApi/BookstoreApi.csproj
COPY src/ src/
RUN dotnet publish src/BookstoreApi/BookstoreApi.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BookstoreApi.dll"]
