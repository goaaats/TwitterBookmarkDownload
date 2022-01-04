using CommandLine;
using PuppeteerSharp;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace TwitterBookmarkDownload;

public class Program
{
    public class Options
    {
        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
        public bool Verbose { get; set; }

        [Option('c', "cookie-file", Required = false, HelpText = "Path to file containing cookies.", Default = "cookies.txt")]
        public string CookieFile { get; set; }

        [Option('o', "output-path", Required = false, HelpText = "Path to output folder.", Default = "out")]
        public string OutputPath { get; set; }
    }

    private static Regex bookmarksApiRegex =
        new Regex("\\/i\\/api\\/graphql\\/(.*)\\/Bookmarks", RegexOptions.Compiled);

    private static HttpClient httpClient = new();

    public static string? GetCommandPath(string exeName)
    {
        try
        {
            using Process p = new Process();

            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = "where";
            p.StartInfo.Arguments = exeName;
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            return p.ExitCode != 0 ? null : output.Substring(0, output.IndexOf(Environment.NewLine, StringComparison.InvariantCulture));

            // just return first match
        }
        catch (Win32Exception)
        {
            throw new Exception("'where' command is not on path");
        }
    }

    private static int numDownloaded = 0;
    private static int numExisting = 0;

    private static async Task DownloadEntry(GraphModel.InstructionItemContent content, string outPath, string[] existingFiles)
    {
        if (content.TweetResults.Result.Legacy.ExtendedEntities != null)
        {
            for (var i = 0; i < content.TweetResults.Result.Legacy.ExtendedEntities.Media.Count; i++)
            {
                var media = content.TweetResults.Result.Legacy.ExtendedEntities.Media[i];

                numExisting++;

                var filename = $"{content.TweetResults.Result.Core.UserResults.Result.Legacy.ScreenName}-{content.TweetResults.Result.RestId}-{i}";
                if (existingFiles.Any(x => x.Contains(filename)))
                {
                    Console.WriteLine($"\t=> Already downloaded: {filename}");
                    continue;
                }

                switch (media.Type)
                {
                    case "photo":
                        var data = await httpClient.GetByteArrayAsync(media.MediaUrlHttps);
                        await File.WriteAllBytesAsync(Path.Combine(outPath, filename + "." + media.MediaUrlHttps.Split(".").Last()), data);
                        break;

                    case "animated_gif":
                    case "video":
                        var dlpPath = GetCommandPath("yt-dlp");
                        if (dlpPath == null)
                            throw new Exception("yt-dlp is not on path, but you have a video in your bookmarks!");

                        var si = new ProcessStartInfo(dlpPath)
                        {
                            Arguments = $"--output {Path.Combine(outPath, filename)}.%(ext)s {media.Url}"
                        };
                        var p = Process.Start(si)!;
                        await p.WaitForExitAsync();

                        if (p.ExitCode != 0)
                            throw new Exception($"yt-dlp failed for {media.Url}");

                        // Find the file, so that we can set the file time
                        break;

                    default:
                        throw new ArgumentException($"Unknown media type {media.Type}");
                }

                Console.WriteLine($"\t=> Downloaded restId#{content.TweetResults.Result.RestId} screenName#{content.TweetResults.Result.Core.UserResults.Result.Legacy.ScreenName} media#{i} mediaType#{i}");
                numDownloaded++;
            }
        }
    }

    private static ConcurrentQueue<GraphModel.Root> toDownloadQueue = new();
    private static CancellationTokenSource downloadThreadCancel = new();

    private static void DownloadThreadStart(object? outPath)
    {
        var existingFiles = Directory.GetFiles((string)outPath);
        var dlCounter = 1;
        while (!downloadThreadCancel.Token.IsCancellationRequested)
        {
            if (toDownloadQueue.TryDequeue(out var body) && body != null)
            {
                int index = 0;

                foreach (var instructionContent in body.Data.BookmarkTimeline.Timeline.Instructions.SelectMany(x => x.Entries).Select(x => x.Content.ItemContent).Where(x => x != null && x.ItemType == "TimelineTweet"))
                {
                    if (instructionContent.TweetResults == null || instructionContent.TweetResults.Result == null)
                    {
                        Console.WriteLine("\t=> Deleted tweet in timeline, skipping.");
                        continue;
                    }

                    DownloadEntry(instructionContent, (string)outPath, existingFiles).GetAwaiter().GetResult();
                    index++;
                }

                Console.WriteLine("=> Downloaded page " + dlCounter++);
            }
        }
    }

    public static async Task Main(string[] args)
    {
        await Parser.Default.ParseArguments<Options>(args).WithParsedAsync(async o =>
        {
            if (!File.Exists(o.CookieFile))
                throw new ArgumentException("Cookie file does not exist.");

            Directory.CreateDirectory(o.OutputPath);

            var dlThread = new Thread(DownloadThreadStart);
            dlThread.Start(o.OutputPath);

            var cookies = await File.ReadAllTextAsync(o.CookieFile);
            var cookieParams = cookies.Split("; ").Where(x => !string.IsNullOrEmpty(x)).Select(x =>
            {
                var param = new CookieParam();
                var split = x.Split("=");
                param.Name = split[0];
                param.Value = split[1];
                param.Domain = "twitter.com";

                return param;
            }).ToArray();

            using var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();
            await using var browser = await Puppeteer.LaunchAsync(
                new LaunchOptions { Headless = false });
            await using var page = await browser.NewPageAsync();

            Console.WriteLine("Waiting for bookmarks page to load...");

            page.RequestFinished += async (sender, eventArgs) =>
            {
                if (!bookmarksApiRegex.IsMatch(eventArgs.Request.Url))
                    return;

                if (eventArgs.Request.Response.Status != HttpStatusCode.OK || !eventArgs.Request.Response.Ok)
                    throw new Exception("Twitter API returned an error. Please try again!");

                var body = await eventArgs.Request.Response.JsonAsync<GraphModel.Root>();

                if (body.Data.BookmarkTimeline.Timeline == null)
                {
                    Console.WriteLine("Twitter returned an error - if there's no more downloads after this, please try again.");
                    return;
                }

                toDownloadQueue.Enqueue(body);
            };

            await page.SetCookieAsync(cookieParams);
            await page.GoToAsync("https://twitter.com/i/bookmarks", WaitUntilNavigation.DOMContentLoaded);

            var needLogin = await page.EvaluateExpressionAsync<bool>("document.querySelector(\"a[href='/login']\") != null");
            if (needLogin)
                throw new Exception("You need to login to Twitter - your cookies might not be valid.");

            Console.WriteLine("Waiting for first tweet to load...");
            try
            {
                await page.WaitForSelectorAsync("[data-testid='tweet']");
            }
            catch (WaitTaskTimeoutException)
            {
                Console.WriteLine("Twitter didn't send the initial timeline, please try again.");
                await browser.CloseAsync();
                return;
            }

            Console.WriteLine("Scrolling to the bottom...");

            Console.CancelKeyPress += async (sender, eventArgs) => 
            {
                browser.CloseAsync();

                Thread.Sleep(500);
                if (dlThread is {IsAlive: true})
                {
                    downloadThreadCancel.Cancel();
                    dlThread.Join();
                }

                Environment.Exit(0);
            };

            const int distance = 100;
            var totalHeight = 0;
            var scrollHeight = await page.EvaluateExpressionAsync<int>("document.body.scrollHeight");

            while (true)
            {
                while (totalHeight < scrollHeight)
                {
                    await page.EvaluateExpressionAsync($"window.scrollBy(0, {distance})");
                    await Task.Delay(100);

                    scrollHeight = await page.EvaluateExpressionAsync<int>("document.body.scrollHeight");
                    totalHeight += distance;
                }

                Console.WriteLine("Scrolled to bottom, waiting to see if another request is coming in...");
                // Wait to see if another request is just coming in
                Thread.Sleep(4000);

                // TODO: Check if there is a retry button here, if there is, click it and wait

                scrollHeight = await page.EvaluateExpressionAsync<int>("document.body.scrollHeight");
                if (totalHeight >= scrollHeight)
                    break;
            }

            Console.WriteLine("Scroll done.");

            // Wait for download & scroll thread to finish up
            while (!toDownloadQueue.IsEmpty)
            {
                Thread.Sleep(1);
            }

            downloadThreadCancel.Cancel();
            dlThread.Join();
            Console.WriteLine($"Done!\nNumber of photos/videos: {numExisting}\nActually downloaded: {numDownloaded}\n\nTake care! Twitter is extremely unreliable and might have indicated to us that your bookmarks timeline has ended earlier than it actually did, you may have to run the downloader again.");

            Thread.Sleep(2000);

            await browser.CloseAsync();
        });
    }
}