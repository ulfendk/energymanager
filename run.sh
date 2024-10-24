#!/usr/bin/with-contenv bashio
echo "Starting nginx..."
nginx -g "daemon off;error_log /dev/stdout debug;" &

echo "Starting Energy Manager [DK]..."
dotnet exec EnergyManager.Backend.dll