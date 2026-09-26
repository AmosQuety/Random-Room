# Stage 1: build the React UI into wwwroot
FROM node:22-alpine AS web
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# Stage 2: publish the API together with the built UI
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY backend/RandomRoom.Api/RandomRoom.Api.csproj backend/RandomRoom.Api/
RUN dotnet restore backend/RandomRoom.Api
COPY backend/RandomRoom.Api backend/RandomRoom.Api
COPY --from=web /src/backend/RandomRoom.Api/wwwroot backend/RandomRoom.Api/wwwroot
RUN dotnet publish backend/RandomRoom.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app .
# Render terminates TLS in front of us; trust its forwarded client address for rate limiting.
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
# Render tells the container which port to listen on via $PORT.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} exec dotnet RandomRoom.Api.dll"]
