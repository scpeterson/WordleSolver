FROM node:22-bookworm-slim AS client-build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY WordleSolver.Elm ./WordleSolver.Elm
COPY WordleSolver/wwwroot ./WordleSolver/wwwroot
COPY scripts ./scripts
RUN npm run check:client


FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WordleSolver/WordleSolver.fsproj ./WordleSolver/
RUN dotnet restore WordleSolver/WordleSolver.fsproj

COPY WordleSolver ./WordleSolver
COPY --from=client-build /src/WordleSolver/wwwroot/app.js ./WordleSolver/wwwroot/app.js
RUN dotnet publish WordleSolver/WordleSolver.fsproj --configuration Release --output /app/publish


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "WordleSolver.dll"]
