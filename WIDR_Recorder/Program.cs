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
using CommandLine;

namespace StreamRecorder
{
    class Program
    {
        public class Options
        {
            [Option("start", Required = false, HelpText = "Set the start time in 24-hour format (e.g., 23:00 for 11pm).")]
            public string StartTime { get; set; }

            [Option("duration", Required = false, HelpText = "Set the duration in minutes.", Default = 0)]
            public int Duration { get; set; }
            
            [Option("day", Required = false, HelpText = "Set the day of the week to start (e.g., Monday).")]
            public string DayOfWeek { get; set; }
            
            [Option("timezone", Required = false, HelpText = "Set the timezone (e.g., 'America/New_York').", Default = "America/New_York")]
            public string Timezone { get; set; }
        }

        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            { 
                args = new[]
                {
                    "--day=friday",
                    "--start=11:00pm",
                    "--duration=70",
                    "--timezone=America/New_York"
                };
            }

            await Parser.Default.ParseArguments<Options>(args).WithParsedAsync(async options =>
            {
                Console.WriteLine(options.DayOfWeek);
                Console.WriteLine(options.Timezone);
                
                DateTimeOffset startTime = DateTimeOffset.Parse(options.StartTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
                TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Timezone);

                startTime = TimeZoneInfo.ConvertTime(startTime, timeZone);

                if (options.DayOfWeek != null)
                {
                    DayOfWeek dayOfWeek = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), options.DayOfWeek, true);
                    int daysToAdd = ((int)dayOfWeek - (int)startTime.DayOfWeek + 7) % 7;
                    startTime = startTime.Date.AddDays(daysToAdd).Add(startTime.TimeOfDay);
                }
                
                TimeSpan duration = options.Duration == 0 ? TimeSpan.FromSeconds(10) : TimeSpan.FromMinutes(options.Duration);
                while (true)
                {
                    TimeSpan timeToWait = startTime - DateTime.Now;

                    if (timeToWait > TimeSpan.Zero)
                    {
                        Console.WriteLine($"[{DateTime.Now.ToString("g")} ({TimeZoneInfo.Local.DisplayName})] Waiting until {startTime.ToString("g")} {startTime.LocalDateTime} ({options.Timezone}) to start recording...");
                        await Task.Delay(timeToWait);
                    }

                    Console.WriteLine($"[{DateTime.Now.ToString("g")} ({TimeZoneInfo.Local.DisplayName})] Recording the stream...");
                    await RecordStream(duration);
                    Console.WriteLine($"[{DateTime.Now.ToString("g")} ({TimeZoneInfo.Local.DisplayName})] Recording completed.");
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
                        Console.WriteLine($"[{DateTime.Now.ToString("g")} ({TimeZoneInfo.Local.DisplayName})] Saved to {filePath}");
                    }
                }
            }
        }
    }
}
