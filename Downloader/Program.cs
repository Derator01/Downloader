using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

public static partial class Program
{
    private static async Task Main()
    {
        //string websiteUrl = @"https://artsandculture.google.com/story/6AVRwL3GipL7LQ" /*Console.ReadLine()*/;
        string downloadFolderPath = "DownloadedImages";

        if (!Directory.Exists(downloadFolderPath))
            Directory.CreateDirectory(downloadFolderPath);

        ChromeOptions options = new ChromeOptions();
        options.AddArgument("--start-maximized");

        using (IWebDriver driver = new ChromeDriver(options))
        {
            // Navigate to the page
            driver.Navigate().GoToUrl(Console.ReadLine());

            // Wait for the dynamic content to be loaded (you may need to adjust the wait condition)
            // For example, wait for a specific element to be present
            for (int i = 0; i < 40; i++)
            {
                Thread.Sleep(400);
                ScrollPage(driver, 1000);
            }

            var divElements = driver.FindElements(By.XPath("//div[@style]"));
            string regexPattern = @"(?<=background-image:\s*url\()[^)]+(?<!Soften=[^)]+)(?=\);)";
            Regex urlRegex = new Regex(@"\bhttps?://\S+\b");

            List<string> matchedStrings = new();

            foreach (var div in divElements)
            {
                string style = div.GetAttribute("style");
                MatchCollection matches = urlRegex.Matches(style);

                // Check if there is at least one valid URL
                if (matches.Count > 0)
                {
                    string cleanedStyle = Regex.Replace(style, regexPattern, match =>
                    {
                        // Replace Soften attribute with empty string
                        return Regex.Replace(match.Value, @"\s*Soften=\d+(?:,\d+){2}-\S+", "");
                    });

                    matchedStrings.Add(cleanedStyle);
                    //Console.WriteLine("Cleaned style with valid URL(s): " + cleanedStyle);
                }
            }

            string pattern = @"background-image:\s*url\(""([^""']*)(?<!-Soften)""\);";
            for (int i = 0; i < matchedStrings.Count; i++)
            {
                var smth = Regex.Match(matchedStrings[i], pattern);
                string value = smth.Groups[1].Value;
                matchedStrings[i] = value[..value.IndexOf('=')];
                await Console.Out.WriteLineAsync(value[..value.IndexOf('=')]);
            }

            await DownloadImagesAsync(new HttpClient(), "", matchedStrings, downloadFolderPath);
        }

        Console.WriteLine("All images downloaded successfully.");
    }

    static void ScrollPage(IWebDriver driver, int yOffset)
    {
        // Execute JavaScript to scroll the page
        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
        js.ExecuteScript($"window.scrollBy(0, {yOffset});");
    }

    private static async Task<string> DownloadHtmlContentAsync(HttpClient httpClient, string websiteUrl)
    {
        var byteArray = await httpClient.GetByteArrayAsync(websiteUrl);
        string htmlContent = Encoding.UTF8.GetString(byteArray);
        return htmlContent;
    }

    private static List<string> ExtractUrlsFromHtml(string htmlContent, string pattern)
    {
        List<string> urls = new List<string>();
        Regex regex = new(pattern, RegexOptions.IgnoreCase);

        MatchCollection matches = regex.Matches(htmlContent);
        foreach (Match match in matches.Cast<Match>())
            try
            {
                if (match.Groups[1].Value[..^23] == "https://lh3.googleusercontent.com/ci/AE9Axz9nycFPo1BSvI1QFZ9q5e_pPNW58Qv38M4I8cR5AAKgrzWFcadtA4DwmJRxCRGve1kV9Dm1KYw=w1200-c-h539")
                {
                    urls.Add(match.Groups[1].Value[..^23]);
                }
                Console.WriteLine(match.Groups[1].Value + '\n');
            }
            catch { }
        return urls;
    }

    private static async Task DownloadImagesAsync(HttpClient httpClient, string baseUrl, List<string> urls, string downloadFolderPath)
    {
        foreach (var imageUrl in urls)
        {
            string completeImageUrl = imageUrl.StartsWith("http") || imageUrl.StartsWith("https") ? imageUrl : baseUrl + imageUrl;

            // Download the image
            byte[] imageBytes = Array.Empty<byte>();
            try
            {
                imageBytes = await httpClient.GetByteArrayAsync(completeImageUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                continue; // Skip to the next image if download fails
            }

            // Get image dimensions
            using (var ms = new MemoryStream(imageBytes))
            {
                using (var image = System.Drawing.Image.FromStream(ms))
                {
                    int originalWidth = image.Width;
                    int originalHeight = image.Height;

                    // Construct new URL with dimensions doubled
                    string newImageUrl = $"{completeImageUrl}=w{originalWidth * 4}-h{originalHeight * 4}";

                    // Download the image with doubled dimensions
                    byte[] resizedImageBytes = await httpClient.GetByteArrayAsync(newImageUrl);

                    // Save the resized image
                    string fileName = GetUniqueFileName(resizedImageBytes, ".jpg");
                    string filePath = Path.Combine(downloadFolderPath, fileName);
                    await File.WriteAllBytesAsync(filePath, resizedImageBytes);
                    Console.WriteLine($"Downloaded: {fileName}");
                }
            }
        }
    }

    private static string GetFileExtension(string contentType)
    {
        switch (contentType)
        {
            case "image/jpeg":
                return ".jpg";
            case "image/png":
                return ".png";
            case "image/gif":
                return ".gif";
            case "image/webp":
                return ".webp";
            default:
                return ".jpg";
        }
    }

    private static string GetUniqueFileName(byte[] data, string fileExtension)
    {
        using (var md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(data);
            StringBuilder stringBuilder = new StringBuilder();

            foreach (byte b in hash)
            {
                stringBuilder.Append(b.ToString("x2"));
            }

            return stringBuilder.ToString() + fileExtension;
        }
    }

    [GeneratedRegex("\\s*Soften=\\d+(?:,\\d+){2}-\\S+")]
    private static partial Regex MyRegex();
}
