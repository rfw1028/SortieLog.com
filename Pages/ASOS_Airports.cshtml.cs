using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using FlightLogs.Hubs;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace FlightLogs.Pages
{
    [Authorize]
    public class ASOS_AirportsModel : PageModel
    {
        public void OnGet()
        {
        }

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHubContext<ProgressHub> _hubContext;
        private readonly IConfiguration _config;

        public ASOS_AirportsModel(IHttpClientFactory httpClientFactory, IHubContext<ProgressHub> hubContext, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _hubContext = hubContext;
            _config = config;
        }


        public async Task<IActionResult> OnGetProxyAirport(string icao)
        {
            string apiUrl = "https://aviationweather.gov/api/data/airport?ids=" + icao + "&format=json";

            try
            {
                var client = _httpClientFactory.CreateClient();

                var response = await client.GetAsync(apiUrl);

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();

                return Content(json, "application/json");
            }
            catch (Exception ex)            
            {
               
                return new JsonResult(new { error = ex.Message });
            }
        }
                

        public class ProgressRequest
        {
            public string? ConnectionId { get; set; }
        }


        public class MapBounds
        {
            public double North { get; set; }
            public double East { get; set; }
            public double South { get; set; }
            public double West { get; set; }
        }

        public class icaoRequest
        {
            public string? icao { get; set; }
            public string? date { get; set; }
            public string? connectionId { get; set; }
            public double lat { get; set; }
            public double lng { get; set; }
        }


        public async Task<IActionResult> OnPostWeather([FromBody] icaoRequest request)
        {
            int Mo = 0;
            int Dy = 0;
            string qry = string.Empty;

            try
            {
                string? avgWindDir = "";
                string? avgWindSpd = "";
                string? avgTemp = string.Empty;
                string? avgBaro = string.Empty;

                string? date = request.date;
                string? connId = request.connectionId;
                var lat = request.lat;
                var lng = request.lng;

                if (lat == 0 || lng == 0)
                {
                    return new JsonResult(new { message = "Lat and Lng are null." });
                }

                DateTime parsedDate;
                if (!DateTime.TryParse(date, out parsedDate))
                    return new JsonResult(new { message = "Invalid date format." });

                Mo = parsedDate.Month;
                Dy = parsedDate.Day;

                var cts = new CancellationTokenSource();
                var token = cts.Token;

                // Fire-and-forget progress loop
                _ = Task.Run(async () =>
                {
                    int step = 0;
                    while (!token.IsCancellationRequested && step <= 90)
                    {
                        await _hubContext.Clients.Client(connId)
                            .SendAsync("ReceiveProgress", step, $"Working... {step}%");
                        step += 1;
                        await Task.Delay(20);
                    }
                }, token);

                // Actual query
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("WxClean") ?? throw new InvalidOperationException("ConnectionStrings:WxClean is not configured. See README.md.")))
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandTimeout = 120;
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandText = @"
                SELECT 
                    AVG(TRY_CAST([WindDir] AS INT)) AS AvgWindDir,
                    AVG(TRY_CAST([WindSpd] AS INT)) AS AvgWindSpd,
                    AVG(TRY_CAST([Temp] AS INT)) AS AvgTemp,
                    AVG(TRY_CAST([Baro1] AS FLOAT)) AS AvgBaro
                FROM (
                  SELECT 
    TRY_CAST([2minuteWindDir] AS INT) AS WindDir,
    TRY_CAST([2MinuteWindSpd] AS INT) AS WindSpd,
    TRY_CAST([Temp] AS INT) AS Temp,
    TRY_CAST([Baro1] AS FLOAT) AS Baro1
FROM asos2024
                    WHERE [month] = @month AND [day] = @day
                        AND GeoPoint.STDistance(geography::Point(@lat, @lon, 4326)) < @radius
                        AND [2minuteWindDir] <> 'm' AND [2minuteWindSpd] <> 'm'
                    UNION ALL
                   SELECT 
    TRY_CAST([2minuteWindDir] AS INT) AS WindDir,
    TRY_CAST([2MinuteWindSpd] AS INT) AS WindSpd,
    TRY_CAST([Temp] AS INT) AS Temp,
    TRY_CAST([Baro1] AS FLOAT) AS Baro1
FROM asos2023
                    WHERE [month] = @month AND [day] = @day
                        AND GeoPoint.STDistance(geography::Point(@lat, @lon, 4326)) < @radius
                        AND [2minuteWindDir] <> 'm' AND [2minuteWindSpd] <> 'm'
                    UNION ALL
                   SELECT 
    TRY_CAST([2minuteWindDir] AS INT) AS WindDir,
    TRY_CAST([2MinuteWindSpd] AS INT) AS WindSpd,
    TRY_CAST([Temp] AS INT) AS Temp,
    TRY_CAST([Baro1] AS FLOAT) AS Baro1
FROM asos2022
                    WHERE [month] = @month AND [day] = @day
                        AND GeoPoint.STDistance(geography::Point(@lat, @lon, 4326)) < @radius
                        AND [2minuteWindDir] <> 'm' AND [2minuteWindSpd] <> 'm'
                ) AS combined";

                    cmd.Parameters.AddWithValue("@month", Mo);
                    cmd.Parameters.AddWithValue("@day", Dy);
                    cmd.Parameters.AddWithValue("@lat", lat);
                    cmd.Parameters.AddWithValue("@lon", lng);
                    cmd.Parameters.AddWithValue("@radius", 80000);

                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            avgWindDir = reader["AvgWindDir"]?.ToString();
                            avgWindSpd = reader["AvgWindSpd"]?.ToString();
                            avgTemp = reader["AvgTemp"]?.ToString();
                            avgBaro = reader["AvgBaro"]?.ToString();
                        }
                    }
                }

                cts.Cancel(); // Stop progress loop

                await _hubContext.Clients.Client(connId)
                    .SendAsync("ReceiveProgress", 100, "Query complete ✅");
                Console.WriteLine($"Returning: {Mo}/{Dy}, WindDir: {avgWindDir}, WindSpd: {avgWindSpd}, Temp: {avgTemp}, Baro: {avgBaro}");
                return new JsonResult(new
                {                   
                    date = $"{Mo}/{Dy}",
                    avgWindDir,
                    avgWindSpd,
                    avgTemp,
                    avgBaro
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { message = "Weather lookup failed." });
            }
        }





        public async Task<IActionResult> OnPostBoundsAsync([FromBody] MapBounds bounds)
        {
            try
            {
                string json;
                double north = bounds.North;
                double west = bounds.West;
                double east = bounds.East;
                double south = bounds.South;

                string apiUrl= "https://aviationweather.gov/api/data/airport?bbox=" + south + "%2C" + west + "%2C" + north + "%2C" + east + "&format=json";

                var client = _httpClientFactory.CreateClient();

                var response = await client.GetAsync(apiUrl);                

               json = await response.Content.ReadAsStringAsync();

                return Content(json, "application/json");
             
            }catch(Exception ex)
            {
                return new JsonResult(new { message = ex.Message });
            }
        }

        public class AirportRoot
        {
            public Airport[]? Airports { get; set; }
        }

        public class Airport
        {
            public string? ICAO { get; set; }
            public float latitude { get; set; }
            public float longitude { get; set; }
        }
               

    }

}
