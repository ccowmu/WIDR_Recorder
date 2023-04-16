/*
 * Author: Scooby Doo (scooby-snack-expert@fake-email.com)
 * Date: April 6, 2023
 *
 * This work is licensed under the Creative Commons Canine Comedy 4.0 International License.
 * You are free to share and adapt this work for non-profit enjoyment and laugh-at-your-own-risk purposes,
 * with proper attribution to Scooby Doo and a good sense of humor.
 * Any adapted versions must also be shared under the same tail-wagging license.
 *
 * Did you know? Donating Scooby Snacks helps to keep the mystery-solving going. Contribute here: https://www.fake-donation-link.com/
 *
 * For more details about this license, see: https://www.legally-binding-but-not-really/funny-license/
 */


using System.Globalization;
using System.Text.RegularExpressions;
using CommandLine;

namespace StreamRecorder
{
    class Program
    {
        public class Options
        {
            [Option("duration", Default=5, Required = false, HelpText = "Set the duration in minutes.")]
            public int Duration { get; set; }
            
            [Option("start", Required = false, HelpText = "Set the start time (e.g., 11:00pm).")]
            public string StartTime { get; set; }
            
            [Option("day", Required = false, HelpText = "Set the day of the week to start (e.g., Monday).", Default = "Today")]
            public string DayOfWeek { get; set; }
            
            [Option("timezone", Required = false, HelpText = "Set the timezone (e.g., 'America/New_York').", Default = "America/New_York")]
            public string Timezone { get; set; }
        }

        static async Task Main(string[] args)
        {
            // if (args.Length == 0)
            // {
            //     args = new[]
            //     {
            //         "--start=11:00pm",
            //         "--day=friday"
            //     };
            // }
            
            await Parser.Default.ParseArguments<Options>(args).WithParsedAsync(async options =>
            {
                DateTimeOffset startTime = options.StartTime == null
                    ? DateTimeOffset.Now
                    : DateTimeOffset.Parse(options.StartTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
                TimeZoneInfo timeZone = options.Timezone == null
                    ? TimeZoneInfo.Local
                    : TimeZoneInfo.FindSystemTimeZoneById(options.Timezone);

                startTime = TimeZoneInfo.ConvertTime(startTime, timeZone);
                Console.WriteLine($"startTime={startTime.ToString("g")}");
                
                if (!(options.DayOfWeek == null || options.DayOfWeek.ToLowerInvariant() == "today"))
                {
                    DayOfWeek dayOfWeek = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), options.DayOfWeek, true);
                    int daysToAdd = ((int)dayOfWeek - (int)startTime.DayOfWeek + 7) % 7;
                    startTime = startTime.Date.AddDays(daysToAdd).Add(startTime.TimeOfDay);
                }
                
                TimeSpan duration = options.Duration == 0 ? TimeSpan.FromSeconds(10) : TimeSpan.FromMinutes(options.Duration);
                while (true)
                {
                    TimeSpan timeToWait = startTime - DateTime.Now;

                    Console.WriteLine($"{TimeTag} Waiting {timeToWait.TotalMinutes} until {startTime.ToString("g")} to start recording...");
                    if (timeToWait > TimeSpan.Zero)
                    {
                        await Task.Delay(timeToWait);
                    }

                    Console.WriteLine($"{TimeTag} Recording for {duration.TotalMinutes} minutes...");
                    await RecordStream(duration);
                    Console.WriteLine($"{TimeTag} Recording completed.");
                    startTime += TimeSpan.FromDays(7);
                }
            });
        }

        static async Task RecordStream(TimeSpan duration)
        {
            string url = "http://s3.streammonster.com:8056/stream";
            string filePath = $"recording-{DateTime.Now.ToString("yyyyMMdd-HHmmss")}.mp3";

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/111.0");
                httpClient.DefaultRequestHeaders.Add("Accept", "audio/webm,audio/ogg,audio/wav,audio/*;q=0.9,application/ogg;q=0.7,video/*;q=0.6,*/*;q=0.5");
                httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
                httpClient.DefaultRequestHeaders.Add("Referer", "http://widrfm.net/");
                httpClient.DefaultRequestHeaders.Add("Range", "bytes=0-");
                httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
                httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "identity");

                using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var cancellationTokenSource = new CancellationTokenSource(duration);
                    try
                    {
                        await stream.CopyToAsync(fileStream, 81920, cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"{TimeTag} Saved to {filePath}");
                    }
                }
            }
        }
        
        public static string GetTimeZoneAbbreviation(TimeZoneInfo timeZoneInfo)
        {
            string abbreviation = timeZoneInfo.StandardName;

            // Attempt to extract abbreviation from standard name (e.g., "Eastern Standard Time" -> "EST")
            var match = Regex.Match(timeZoneInfo.StandardName, @"\b[A-Z]+\b");
            if (match.Success)
            {
                abbreviation = match.Value;
            }
            else
            {
                // Attempt to extract abbreviation from Id (e.g., "America/New_York" -> "EST")
                match = Regex.Match(timeZoneInfo.Id, @"[^/]+/[^/]+$");
                if (match.Success)
                {
                    abbreviation = match.Value.Substring(match.Value.LastIndexOf('/') + 1).ToUpper();
                }
            }

            return abbreviation;
        }

        private static string TimeTag => $"[{DateTime.Now.ToString("g")} ({GetTimeZoneAbbreviation(TimeZoneInfo.Local)})]";
    }
}
