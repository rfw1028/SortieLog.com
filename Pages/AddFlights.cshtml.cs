using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using System.Runtime.Intrinsics.X86;
using System.Data;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace FlightLogs.Pages
{

    [Authorize]
  
    public class AddFlightsModel : PageModel
    {
        public string? UserName { get; private set; }
        public string? UserEmail { get; private set; }

        string jsonData = string.Empty;
        string wx = string.Empty;
        private readonly IConfiguration _config;
        private readonly ILogger<AddFlightsModel> _logger;

        public AddFlightsModel(IConfiguration config, ILogger<AddFlightsModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        private string ApiToken => _config["FlightRadar24:ApiToken"]
            ?? throw new InvalidOperationException("FlightRadar24:ApiToken is not configured. See README.md.");

        private string ConnectionString => _config.GetConnectionString("FlightLogs")
            ?? throw new InvalidOperationException("ConnectionStrings:FlightLogs is not configured. See README.md.");

        string track = "";

        public void OnGet()
        {
           UserEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            UserName = User.FindFirst(ClaimTypes.Name)?.Value;
        }

        public string? MarkerJson { get; set; }

        public class RequestData
        {
            public string? Data { get; set; }
        }

        public class ResponseData
        {
            public string? Message { get; set; }
        }

        public async Task<JsonResult> OnPostSaveFlight()
        {
            string saveMessage = "";
            try
            {
                string jsonString;
                using (var reader = new StreamReader(Request.Body))
                {
                    jsonString = await reader.ReadToEndAsync();
                }

                FlightInfo? flt = JsonSerializer.Deserialize<FlightInfo>(jsonString);

                if (flt == null)
                {
                    return new JsonResult(new { message = "Conversion failed." });
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                DateTime startTime = flt.startTime;
                DateTime endTime = flt.endTime;
                string? wx = HttpContext.Session.GetString("FlightWeather");
                string? track = HttpContext.Session.GetString("FlightTrack");
                string? airplane = flt.airplane;
                DateTime eventDate = flt.eventDate;
                string? FR24ID = flt.fr24_id;


                startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
                endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
                eventDate = DateTime.SpecifyKind(eventDate, DateTimeKind.Utc);

                startTime = startTime.ToLocalTime();
                endTime = endTime.ToLocalTime();
                eventDate = eventDate.ToLocalTime();

                var email = User.FindFirst(ClaimTypes.Email)?.Value;

                //saveMessage = "StartTime" + startTime + " EndTime: " + endTime + " wx: " + wx + " track: " + track + " airplane: " + airplane + " eventDate: " + eventDate;

                

                if (email == null)
                {
                    saveMessage = "Pilot email is blank.";
                }
                else
                {

                    TimeSpan ts = endTime - startTime;
                    int tt = (int)ts.TotalMinutes;
                    FR24ID = HttpContext.Session.GetString("FR24ID");
                    await using (SqlConnection conn = new SqlConnection(ConnectionString))
                    {
                        await conn.OpenAsync();

                        using (SqlCommand exists = new SqlCommand(
                            "SELECT COUNT(*) FROM [FlightLogs].[dbo].[Events] WHERE [fr24_id] = @id", conn))
                        {
                            exists.Parameters.AddWithValue("@id", (object?)FR24ID ?? DBNull.Value);
                            int count = (int)(await exists.ExecuteScalarAsync())!;

                            if (count > 0)
                            {
                                //don't insert, already exists
                                saveMessage = "Flight " + FR24ID + " already exists in database";
                            }
                            else
                            {
                                using (SqlCommand insert = new SqlCommand(
                                    "INSERT INTO [FlightLogs].[dbo].[Events] ([fr24_id],[PilotEmailAddress],[Weather],[Track],[Airplane],[StartTime],[EndTime],[TrackTime]) " +
                                    "VALUES (@id, @email, @wx, @track, @airplane, @start, @end, @tt)", conn))
                                {
                                    insert.Parameters.AddWithValue("@id", (object?)FR24ID ?? DBNull.Value);
                                    insert.Parameters.AddWithValue("@email", email);
                                    insert.Parameters.AddWithValue("@wx", (object?)wx ?? DBNull.Value);
                                    insert.Parameters.AddWithValue("@track", (object?)track ?? DBNull.Value);
                                    insert.Parameters.AddWithValue("@airplane", (object?)airplane ?? DBNull.Value);
                                    insert.Parameters.AddWithValue("@start", startTime);
                                    insert.Parameters.AddWithValue("@end", endTime);
                                    insert.Parameters.AddWithValue("@tt", tt);
                                    insert.CommandTimeout = 0;
                                    await insert.ExecuteNonQueryAsync();
                                    saveMessage = "Flight saved to database";
                                }
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                // Log details server-side; do not return SQL text or exception details to the client.
                _logger.LogError(ex, "SaveFlight failed");
                saveMessage = "Error saving flight.";

                return new JsonResult(new { message = saveMessage });
            }
            return new JsonResult(new { message = saveMessage });
        }

        public async Task<JsonResult> OnPostProcessData([FromBody] FlightData flight)
        {
            if (flight.Airplane == null)
            {
                return new JsonResult(new { message = "Airplane cannot be null" });
            }

            string result = await GetFlight(flight.Airplane, flight.FlightDateTime);

            return new JsonResult(new { message = result });
        }
        string FR24ID = string.Empty;

       

        public async Task<string> GetFlight(string airplane, DateTime dte)
        {
            string jsn = "";
            string? content = "-1";
            string? fid = "";
            DateTimeOffset dtO = new DateTimeOffset(dte);
            string fullUrl = "https://fr24api.flightradar24.com/api/historic/flight-positions/light?callsigns=" + Uri.EscapeDataString(airplane);
            fullUrl += "&timestamp=" + dtO.ToUnixTimeSeconds().ToString();
            var client = new HttpClient();
            // Add headers to the client                        
            client.DefaultRequestHeaders.Add("Accept-Version", "v1");
            // Add Authorization Header
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + ApiToken);
            HttpResponseMessage response = await client.GetAsync(fullUrl);

            if (response.IsSuccessStatusCode)
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                jsn = await response.Content.ReadAsStringAsync();
                FR24? fr24 = JsonSerializer.Deserialize<FR24>(jsn, options);
                content = fr24?.data?[0]?.fr24_id;
                HttpContext.Session.SetString("FR24ID", content);                

                if (fr24?.data?[0]?.fr24_id?.Length > 0)
                {

                    var client2 = new HttpClient();
                    string trackURL = "https://fr24api.flightradar24.com/api/flight-tracks?flight_id=" + fr24?.data?[0]?.fr24_id;
                    client2.BaseAddress = new Uri(trackURL);

                    // Add headers to the client                        
                    client2.DefaultRequestHeaders.Add("Accept-Version", "v1");
                    client2.DefaultRequestHeaders.Add("Accept", "application/json");
                    // Add Authorization Header
                    client2.DefaultRequestHeaders.Add("Authorization", "Bearer " + ApiToken);
                    HttpResponseMessage response2 = await client2.GetAsync(trackURL);
                    if (response2.IsSuccessStatusCode)
                    {

                        track = await response2.Content.ReadAsStringAsync();
                        HttpContext.Session.SetString("FlightTrack", track);
                        track = track.TrimStart('[');
                        track = track.TrimEnd(']');
                        track = "{\"flight\": " + track + "}";
                        track = track.Replace("tracks", "positions");
                        content = track;   //"Success!";                      
                        
                    }
                }
            }
            else
            {
                content = response.StatusCode.ToString();
            }

            return content;

        }


        

        public JsonResult OnPostGetWeather([FromBody] PointData point)
        {


            double radius = 150;
            DateTime wxStart = DateTime.Parse(point.starttime.ToString());
            DateTime wxEnd = DateTime.Parse(point.endtime.ToString());

            //wxStart = wxStart.AddHours(-6);
            //wxEnd = wxEnd.AddHours(-6);


            int aMonth = wxStart.Month;
            int aDay = wxStart.Day;
            int aHour = wxStart.Hour;
            int aMinute = wxStart.Minute;

            int bMonth = wxEnd.Month;
            int bDay = wxEnd.Day;
            int bHour = wxEnd.Hour;
            int bMinute = wxEnd.Minute;


            int points = point.points;

            TimeSpan ts = wxEnd - wxStart;


            string sTime = aHour.ToString() + aMinute.ToString("00");
            string eTime = bHour.ToString() + bMinute.ToString("00");

            // Table name is built only from an int year (not user text); every value is a parameter.
            using SqlConnection conn = new SqlConnection(ConnectionString);
            using (SqlCommand cmd = conn.CreateCommand())
            {
                cmd.CommandType = System.Data.CommandType.Text;
                string qry = "SELECT (SELECT TOP (@points) [ICAO] AS [Airport],[Year],[Month],[Day],[LocalTime],[VEC],[Day-Night],[2minuteWindDir] AS WindDir,[2minuteWindSpd] AS WindSpd,";
                qry += "[PrecipAmtPerMinute], [Baro1],[Temp],[Dewpoint] FROM [WX_Clean].[dbo].[ASOS" + wxStart.Year.ToString(System.Globalization.CultureInfo.InvariantCulture) + "] ";
                qry += " WHERE [Geopoint].STDistance(GEOGRAPHY::Point(@lat, @lon, 4326))/1609.34 < @radius";
                qry += " AND [Month] = @month AND [Day] = @day AND [LocalTime] >= @sTime AND [LocalTime] <= @eTime";
                qry += " ORDER BY  [Geopoint].STDistance(GEOGRAPHY::Point(@lat, @lon, 4326)), [LocalTime] ASC FOR JSON PATH, ROOT('Observations')) AS JsonOutput";
                cmd.Parameters.AddWithValue("@points", points);
                cmd.Parameters.AddWithValue("@lat", point.latitude);
                cmd.Parameters.AddWithValue("@lon", point.longitude);
                cmd.Parameters.AddWithValue("@radius", radius);
                cmd.Parameters.AddWithValue("@month", aMonth);
                cmd.Parameters.AddWithValue("@day", aDay);
                cmd.Parameters.AddWithValue("@sTime", int.Parse(sTime));
                cmd.Parameters.AddWithValue("@eTime", int.Parse(eTime));
                cmd.CommandTimeout = 0;
                cmd.CommandText = qry;
                conn.Open();
                wx = cmd.ExecuteScalar().ToString();

                HttpContext.Session.SetString("FlightWeather", wx);

            }
            return new JsonResult(new { message = wx });

        }

    }
}


public class TrackRoot
{
    public TrackPoint[]? TrackPoints { get; set; }
}

public class TrackPoint
{
    public float lat { get; set; }
    public float lng { get; set; }
}




public class WXRoot
{
    public WeatherPt[]? WeatherPts { get; set; }
}

public class WeatherPt
{
    public string? Airport { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public int LocalTime { get; set; }
    public string? VEC { get; set; }
    public string? DayNight { get; set; }
    public string? WindDir { get; set; }
    public string? WindSpd { get; set; }
    public string? PrecipAmtPerMinute { get; set; }
    public string? Baro1 { get; set; }
    public string? Temp { get; set; }
    public string? Dewpoint { get; set; }
}


public class FlightInfo
{
    public string? fr24_id { get; set; }
    public DateTime eventDate { get; set; }
    public string? wx { get; set; }
    public string? track { get; set; }
    public string? airplane { get; set; }
    public DateTime startTime { get; set; }
    public DateTime endTime { get; set; }
}

public class PointData
{
    public double latitude { get; set; }
    public double longitude { get; set; }
    public DateTime starttime { get; set; }
    public DateTime endtime { get; set; }
    public int points { get; set; }
}


public class ObservationsRoot
{
    public Observation[]? Observations { get; set; }
}

public class Observation
{
    public string? Airport { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public int LocalTime { get; set; }
    public string? VEC { get; set; }
    public string? DayNight { get; set; }
    public string? WindDir { get; set; }
    public string? WindSpd { get; set; }
    public string? PrecipAmtPerMinute { get; set; }
    public string? Baro1 { get; set; }
    public string? Temp { get; set; }
    public string? Dewpoint { get; set; }
}


public class FlightData
{
    public string? Airplane { get; set; }
    public DateTime FlightDateTime { get; set; }
}


public class FR24
{
    public FR24Response[]? data { get; set; }
}

public class FR24Response
{
    public string? fr24_id { get; set; }
    public string? hex { get; set; }
    public string? callsign { get; set; }
    public float lat { get; set; }
    public float lon { get; set; }
    public int track { get; set; }
    public int alt { get; set; }
    public int gspeed { get; set; }
    public int vspeed { get; set; }
    public string? squawk { get; set; }
    public DateTime timestamp { get; set; }
    public string? source { get; set; }
}


public class Rootobject
{
    public Flight? flight { get; set; }
}

public class Flight
{
    public string? fr24_id { get; set; }
    public Position[]? positions { get; set; }
}

public class Position
{
    public DateTime timestamp { get; set; }
    public float lat { get; set; }
    public float lon { get; set; }
    public int alt { get; set; }
    public int gspeed { get; set; }
    public int vspeed { get; set; }
    public int track { get; set; }
    public string? squawk { get; set; }
    public string? callsign { get; set; }
    public string? source { get; set; }
}

