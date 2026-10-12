# SortieLog

ASP.NET Core (.NET 8) flight logbook. Sign in with Google or Microsoft, look up a flight track
(FlightRadar24), attach historical ASOS weather, and save the flight to SQL Server.

## Configuration

No secrets are stored in this repository. `appsettings.json` contains empty placeholders; supply
real values with **user secrets** (development) or **environment variables** (production).

| Key | Purpose |
|---|---|
| `ConnectionStrings:FlightLogs` | SQL Server database `FlightLogs` |
| `ConnectionStrings:WxClean` | SQL Server database `WX_Clean` (ASOS weather) |
| `FlightRadar24:ApiToken` | FlightRadar24 API bearer token |
| `GoogleMaps:ApiKey` | Maps JavaScript API key (restrict it by HTTP referrer) |
| `Authentication:Google:ClientId` / `ClientSecret` | Google OAuth |
| `Authentication:Microsoft:ClientId` / `ClientSecret` / `TenantId` | Microsoft OAuth |

Development:

```bash
dotnet user-secrets set "ConnectionStrings:FlightLogs" "Server=...;Database=FlightLogs;User Id=...;Password=...;TrustServerCertificate=True"
dotnet user-secrets set "ConnectionStrings:WxClean" "Server=...;Database=WX_Clean;User Id=...;Password=...;TrustServerCertificate=True"
dotnet user-secrets set "FlightRadar24:ApiToken" "..."
dotnet user-secrets set "GoogleMaps:ApiKey" "..."
dotnet user-secrets set "Authentication:Google:ClientId" "..."
dotnet user-secrets set "Authentication:Google:ClientSecret" "..."
dotnet user-secrets set "Authentication:Microsoft:ClientId" "..."
dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "..."
dotnet user-secrets set "Authentication:Microsoft:TenantId" "..."
```

Production: use environment variables with `__` as the separator, e.g.
`ConnectionStrings__FlightLogs`, `Authentication__Google__ClientSecret`.

Use a least-privilege SQL login (not `sa`).
