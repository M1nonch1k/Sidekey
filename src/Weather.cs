using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Web.Script.Serialization;

namespace Sidekey
{
    class CityResult
    {
        public string Name, Label;
        public double Lat, Lon;
        public override string ToString() { return Label; }
    }

    class DayForecast
    {
        public DateTime Date;
        public double Max, Min;
        public int Code;
    }

    class WeatherInfo
    {
        public double Temp, Feels, Humidity, Wind;
        public int Code;
        public List<DayForecast> Days = new List<DayForecast>();
        public DateTime Fetched;
    }

    // Погода от Open-Meteo (бесплатно, без ключа). Отправляется только
    // название города (при поиске) или его координаты.
    static class Weather
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Get(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 10000;
            req.ReadWriteTimeout = 10000;
            req.UserAgent = "Sidekey/0.2";
            // Только gzip: «deflate» от сервера .NET Framework распаковывает с ошибкой.
            req.AutomaticDecompression = DecompressionMethods.GZip;
            using (var resp = req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return reader.ReadToEnd();
        }

        public static List<CityResult> FindCity(string query)
        {
            string url = "https://geocoding-api.open-meteo.com/v1/search?count=8&format=json&language=" + L.Code + "&name=" + Uri.EscapeDataString(query);
            var root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(Get(url));
            var list = new List<CityResult>();
            object results;
            if (root == null || !root.TryGetValue("results", out results) || results == null) return list;
            foreach (Dictionary<string, object> r in (object[])results)
            {
                string name = Str(r, "name");
                var parts = new List<string> { name };
                string admin = Str(r, "admin1"), country = Str(r, "country");
                if (admin != "" && admin != name) parts.Add(admin);
                if (country != "") parts.Add(country);
                list.Add(new CityResult { Name = name, Label = string.Join(", ", parts), Lat = D(r["latitude"]), Lon = D(r["longitude"]) });
            }
            return list;
        }

        public static WeatherInfo Fetch(double lat, double lon)
        {
            string url = string.Format(Inv,
                "https://api.open-meteo.com/v1/forecast?latitude={0:0.####}&longitude={1:0.####}" +
                "&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m" +
                "&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=4&wind_speed_unit=ms",
                lat, lon);
            var root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(Get(url));
            var cur = (Dictionary<string, object>)root["current"];
            var w = new WeatherInfo
            {
                Temp = D(cur["temperature_2m"]),
                Feels = D(cur["apparent_temperature"]),
                Humidity = D(cur["relative_humidity_2m"]),
                Wind = D(cur["wind_speed_10m"]),
                Code = (int)D(cur["weather_code"]),
                Fetched = DateTime.Now
            };
            object dailyObj;
            if (root.TryGetValue("daily", out dailyObj) && dailyObj != null)
            {
                var daily = (Dictionary<string, object>)dailyObj;
                var time = (object[])daily["time"];
                var max = (object[])daily["temperature_2m_max"];
                var min = (object[])daily["temperature_2m_min"];
                var code = (object[])daily["weather_code"];
                for (int i = 0; i < time.Length; i++)
                {
                    if (max[i] == null || min[i] == null || code[i] == null) continue;
                    w.Days.Add(new DayForecast
                    {
                        Date = DateTime.ParseExact((string)time[i], "yyyy-MM-dd", Inv),
                        Max = D(max[i]),
                        Min = D(min[i]),
                        Code = (int)D(code[i])
                    });
                }
            }
            return w;
        }

        public static string Explain(Exception ex)
        {
            while (ex is AggregateException && ex.InnerException != null) ex = ex.InnerException;
            var we = ex as WebException;
            bool offline = false;
            try { offline = !NetworkInterface.GetIsNetworkAvailable(); } catch { }
            if (offline)
                return L.T("weather.offline");
            if (we != null)
            {
                switch (we.Status)
                {
                    case WebExceptionStatus.NameResolutionFailure:
                    case WebExceptionStatus.ConnectFailure:
                    case WebExceptionStatus.Timeout:
                    case WebExceptionStatus.ConnectionClosed:
                    case WebExceptionStatus.SendFailure:
                    case WebExceptionStatus.ReceiveFailure:
                    case WebExceptionStatus.ProxyNameResolutionFailure:
                        return L.T("weather.connectFail");
                    case WebExceptionStatus.ProtocolError:
                        var resp = we.Response as HttpWebResponse;
                        return L.F("weather.httpError", resp != null ? (int)resp.StatusCode : 0);
                }
            }
            return L.F("weather.failed", ex.Message);
        }

        public static string Describe(int code)
        {
            // Коды WMO; близкие варианты (например, 51/53/55) объединены в один текст.
            switch (code)
            {
                case 48: code = 45; break;
                case 53: case 55: code = 51; break;
                case 57: code = 56; break;
                case 67: code = 66; break;
                case 99: code = 96; break;
            }
            string key = "wmo." + code;
            string text = L.T(key);
            return text == key ? L.F("wmo.unknown", code) : text;
        }

        public static string Deg(double t)
        {
            int v = (int)Math.Round(t);
            return (v > 0 ? "+" : v < 0 ? "−" : "") + Math.Abs(v) + "°";
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        static double D(object o) { return Convert.ToDouble(o, Inv); }
    }
}
