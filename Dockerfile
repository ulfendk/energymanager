ARG BUILD_FROM
FROM $BUILD_FROM as base

WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["EnergyManager.Backend/EnergyManager.Backend.fsproj", "EnergyManager.Backend/"]
RUN dotnet restore "EnergyManager.Backend/EnergyManager.Backend.fsproj"
COPY . .
WORKDIR "/src/EnergyManager.Backend"
RUN dotnet build "EnergyManager.Backend.fsproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EnergyManager.Backend.fsproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
ENV ASPNETCORE_ENVIRONMENT=Production
WORKDIR /app
COPY --from=publish /app/publish .

RUN apk add --no-cache aspnetcore8-runtime
COPY run.sh /
RUN chmod a+x /run.sh

RUN \
  apk --no-cache add \
    nginx \
  \
  && mkdir -p /run/nginx 

COPY ingress.conf /etc/nginx/http.d/
    
# Copy data for add-on
COPY run.sh /
RUN chmod a+x /run.sh

CMD [ "/run.sh" ]
