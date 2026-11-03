using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using LlmTornado.Internal.Press.Database;
using LlmTornado.Internal.Press.Database.Models;
using LlmTornado.Internal.Press.Utils;
using MarkdownToMedium;
using Microsoft.EntityFrameworkCore;
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;

namespace LlmTornado.Internal.Press.Publisher;

public static class MediumPublisher
{
    private const int MAX_RETRIES = 2;

    public static async Task<bool> PublishArticleAsync(
        Article article,
        string cookieSid,
        string cookieUid,
        bool headless,
        int dailyPostLimit,
        PressDbContext dbContext)
    {
        Console.WriteLine($"[Medium] 📤 Publishing: {article.Title}");

        // Check if already published
        ArticlePublishStatus? publishStatus = await dbContext.ArticlePublishStatus
            .FirstOrDefaultAsync(p => p.ArticleId == article.Id && p.Platform == "medium");
            
        if (publishStatus?.Status == "Published")
        {
            Console.WriteLine($"[Medium] ℹ️ Already published: {publishStatus.PublishedUrl}");
            return true;
        }
        
        // Create or update status record
        publishStatus ??= new ArticlePublishStatus
        {
            ArticleId = article.Id,
            Platform = "medium",
            Status = "Pending"
        };
        
        // Convert markdown to HTML
        string html;
        try
        {
            html = MarkdownToMediumConverter.Convert(article.Body, InlineCodeFormat.DoubleQuotes);
            Console.WriteLine($"[Medium] ✅ Converted markdown to HTML ({html.Length} chars)");
        }
        catch (Exception ex)
        {
            publishStatus.Status = "Failed";
            publishStatus.LastError = $"Markdown conversion error: {ex.Message}";
            
            if (publishStatus.Id == 0)
                dbContext.ArticlePublishStatus.Add(publishStatus);
                
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"[Medium] ❌ {publishStatus.LastError}");
            return false;
        }
        
        // Parse tags from JSON array
        string[] tags = [];
        try
        {
            if (!string.IsNullOrEmpty(article.Tags))
            {
                tags = JsonSerializer.Deserialize<string[]>(article.Tags) ?? [];
            }
        }
        catch
        {
            // Ignore tag parsing errors
        }
        
        // Sanitize tags - Medium only allows alphanumeric + spaces
        string[] sanitizedTags = tags
            .Select(tag => Regex.Replace(tag, @"[^a-zA-Z0-9\s]", ""))
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Take(5) // Medium allows max 5 tags
            .ToArray();
        
        // Attempt publishing with retries
        for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
        {
            IBrowser? browser = null;
            try
            {
                publishStatus.AttemptCount = attempt;
                publishStatus.LastAttemptDate = DateTime.UtcNow;
                
                Console.WriteLine($"[Medium] 🔄 Attempt {attempt}/{MAX_RETRIES}...");
                
                // Launch browser with stealth plugin
                PuppeteerExtra extra = new PuppeteerExtra();
                extra.Use(new StealthPlugin());
                
                browser = await extra.LaunchAsync(CreateLaunchOptions(headless));
                
                IPage page = await browser.NewPageAsync();
                
                // Set realistic viewport size
                await page.SetViewportAsync(new ViewPortOptions 
                { 
                    Width = 1920, 
                    Height = 1080 
                });
                
                // Set Medium cookies
                List<CookieParam> cookies =
                [
                    new CookieParam
                    {
                        Name = "sid",
                        Value = cookieSid,
                        Domain = ".medium.com",
                        Path = "/",
                        Secure = true,
                        HttpOnly = true
                    },
                    new CookieParam
                    {
                        Name = "uid",
                        Value = cookieUid,
                        Domain = ".medium.com",
                        Path = "/",
                        Secure = true,
                        HttpOnly = true
                    }
                ];
                
                await page.SetCookieAsync(cookies.ToArray());
                
                // Navigate to Medium with realistic timing
                Random random = new Random();
                await page.GoToAsync("https://medium.com/");
                await Task.Delay(random.Next(800, 1500));
                
                // Navigate to new story page
                await page.GoToAsync("https://medium.com/new-story?source=home---two_column_layout_nav-----------------------------------------");
                await Task.Delay(random.Next(1500, 2500));
                
                // Random mouse movement before starting
                await WindMouse.MoveToRandomPositionAsync(page);
                await Task.Delay(random.Next(300, 700));
                
                // Enter title with realistic typing and mouse movement
                string titleSelector = "h3[data-testid='editorTitleParagraph']";
                await page.WaitForSelectorAsync(titleSelector, new WaitForSelectorOptions { Timeout = 10000 });
                IElementHandle? titleElement = await page.QuerySelectorAsync(titleSelector);
                if (titleElement != null)
                {
                    await WindMouse.MoveToElementAsync(page, titleElement);
                    await Task.Delay(random.Next(100, 300));
                    await page.ClickAsync(titleSelector);
                    await Task.Delay(random.Next(200, 400));
                    await TypeRealisticallyAsync(page, article.Title);
                    await Task.Delay(500);
                }
                
                // Prepare and copy HTML to clipboard using JavaScript
                await CopyHtmlToClipboardAsync(page, html);
                await Task.Delay(new Random().Next(300, 600));
                
                // Click into content area and paste with realistic timing and mouse movement
                string contentSelector = "p[data-testid='editorParagraphText']";
                IElementHandle? contentElement = await page.QuerySelectorAsync(contentSelector);
                if (contentElement != null)
                {
                    await WindMouse.MoveToElementAsync(page, contentElement);
                    await Task.Delay(random.Next(100, 300));
                    await page.ClickAsync(contentSelector);
                    await Task.Delay(random.Next(200, 400));
                    
                    // Paste using Ctrl+V with realistic key timing
                    await page.Keyboard.DownAsync("Control");
                    await Task.Delay(random.Next(50, 150));
                    await page.Keyboard.PressAsync("KeyV");
                    await Task.Delay(random.Next(50, 150));
                    await page.Keyboard.UpAsync("Control");
                    await Task.Delay(random.Next(800, 1500));
                }
                
                // Click publish button
                await ClickPublishButtonAsync(page);
                await Task.Delay(1000);
                
                // Fill in description with realistic clear and paste and mouse movement
                // Medium limits descriptions to 140 characters
                string description = TruncateDescriptionTo140Chars(article.Description ?? article.Title);
                
                const string descSelector = "[data-testid='publishBodyInput']";
                await page.WaitForSelectorAsync(descSelector, new WaitForSelectorOptions { Timeout = 10000 });
                IElementHandle? descInput = await page.QuerySelectorAsync(descSelector);
                if (descInput != null)
                {
                    await WindMouse.MoveToElementAsync(page, descInput);
                    await Task.Delay(random.Next(100, 300));
                    await descInput.ClickAsync();
                    await Task.Delay(random.Next(200, 400));
                    await ClearAndPasteAsync(page, description);
                    await Task.Delay(300);
                }
                
                // Add tags with realistic typing and mouse movement
                if (sanitizedTags.Length > 0)
                {
                    const string topicsInputSelector = "[data-testid='publishTopicsInput']";
                    await page.WaitForSelectorAsync(topicsInputSelector, new WaitForSelectorOptions { Timeout = 10000 });
                    IElementHandle? topicsInput = await page.QuerySelectorAsync(topicsInputSelector);
                    if (topicsInput != null)
                    {
                        await WindMouse.MoveToElementAsync(page, topicsInput);
                        await Task.Delay(random.Next(100, 250));
                        await topicsInput.ClickAsync();
                        await Task.Delay(random.Next(200, 400));
                        
                        foreach (string tag in sanitizedTags)
                        {
                            await TypeRealisticallyAsync(page, tag);
                            await Task.Delay(random.Next(50, 150));
                            await page.Keyboard.PressAsync("Enter");
                            await Task.Delay(random.Next(200, 400));
                        }
                    }
                }
                
                // Click final publish button and wait for navigation to the published article
                Console.WriteLine("[Medium] 🔘 Clicking final publish button...");
                
                // Create a task that waits for navigation
                Task<IResponse>? navigationTask = page.WaitForNavigationAsync(new NavigationOptions 
                { 
                    Timeout = 15000,
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
                });
                
                // Click the publish button
                bool publishClicked = await ClickFinalPublishButtonAsync(page);
                
                if (!publishClicked)
                {
                    throw new Exception("Failed to click final publish button");
                }
                
                // Wait for the navigation to complete (redirects to published article)
                try
                {
                    await navigationTask;
                    Console.WriteLine("[Medium] ✅ Navigated to published article");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Medium] ⚠️ Navigation wait issue (may still be successful): {ex.Message}");
                    // Continue anyway, we'll check the URL
                }
                
                // Give it a moment to fully load
                await Task.Delay(1000);
                
                // Get the final URL after redirect
                string publishedUrl = page.Url;
                
                // Validate it's a proper Medium article URL
                if (string.IsNullOrEmpty(publishedUrl) || publishedUrl.Contains("/new-story"))
                {
                    throw new Exception($"Invalid published URL: {publishedUrl}");
                }
                
                publishStatus.Status = "Published";
                publishStatus.PublishedUrl = publishedUrl;
                publishStatus.PlatformArticleId = publishedUrl; // Use URL as ID
                publishStatus.PublishedDate = DateTime.UtcNow;
                publishStatus.LastError = null;
                
                if (publishStatus.Id == 0)
                    dbContext.ArticlePublishStatus.Add(publishStatus);
                
                await dbContext.SaveChangesAsync();
                
                Console.WriteLine($"[Medium] ✅ Published: {publishStatus.PublishedUrl}");
                return true;
            }
            catch (TimeoutException ex)
            {
                publishStatus.LastError = $"Timeout: {ex.Message}";
                Console.WriteLine($"[Medium] ⏱️ Attempt {attempt} timeout: {publishStatus.LastError}");
                
                if (attempt < MAX_RETRIES)
                {
                    await Task.Delay(3000);
                }
            }
            catch (Exception ex)
            {
                publishStatus.LastError = ex.Message;
                Console.WriteLine($"[Medium] ❌ Attempt {attempt} error: {ex.Message}{(ex.InnerException is null ? string.Empty : $", inner exception: {ex.InnerException.Message}")}");
                
                // Check if it's an authentication error
                if (ex.Message.Contains("login") || ex.Message.Contains("unauthorized") || ex.Message.Contains("authentication"))
                {
                    Console.WriteLine($"[Medium] 🔒 Authentication error - check cookies");
                    publishStatus.Status = "Failed";
                    
                    if (publishStatus.Id == 0)
                        dbContext.ArticlePublishStatus.Add(publishStatus);
                    
                    await dbContext.SaveChangesAsync();
                    return false;
                }
                
                if (attempt < MAX_RETRIES)
                {
                    await Task.Delay(3000);
                }
            }
            finally
            {
                if (browser != null)
                {
                    try
                    {
                        await browser.CloseAsync();
                    }
                    catch
                    {
                        // Ignore browser close errors
                    }
                }
            }
        }
        
        // All retries failed
        publishStatus.Status = "Failed";
        
        if (publishStatus.Id == 0)
            dbContext.ArticlePublishStatus.Add(publishStatus);
        
        await dbContext.SaveChangesAsync();
        
        Console.WriteLine($"[Medium] 💥 Failed after {MAX_RETRIES} attempts");
        return false;
    }
    
    private static async Task CopyHtmlToClipboardAsync(IPage page, string html)
    {
        // Escape the HTML for JavaScript
        string escapedHtml = html.Replace("\\", "\\\\").Replace("`", "\\`").Replace("$", "\\$");
        
        // Since MarkdownToMedium v1.0.1 produces the correct format, we just need to add meta tag and copy
        string script = $@"
(function() {{
    'use strict';
    
    // MarkdownToMedium already produces Medium-compatible HTML with <pre>line1<br>line2</pre> format
    const htmlContent = `<meta charset='utf-8'>{escapedHtml}`;
    
    // Copy to clipboard
    const tempElement = document.createElement('div');
    tempElement.style.position = 'absolute';
    tempElement.style.left = '-9999px';
    tempElement.innerHTML = htmlContent;
    document.body.appendChild(tempElement);
    
    const selection = window.getSelection();
    const range = document.createRange();
    range.selectNodeContents(tempElement);
    selection.removeAllRanges();
    selection.addRange(range);
    
    let success = false;
    try {{
        success = document.execCommand('copy');
    }} catch (err) {{
        console.error('Copy command failed:', err);
    }}
    
    selection.removeAllRanges();
    document.body.removeChild(tempElement);
    return success;
}})();
";
        
        bool success = await page.EvaluateExpressionAsync<bool>(script);
        
        if (!success)
        {
            throw new Exception("Failed to copy HTML to clipboard");
        }
        
        Console.WriteLine($"[Medium] ✅ HTML copied to clipboard");
    }
    
    private static async Task<string> GetPublishedUrlAsync(IPage page)
    {
        try
        {
            // Try to extract the published URL from the page
            string url = await page.EvaluateExpressionAsync<string>(@"
                (function() {
                    const links = Array.from(document.querySelectorAll('a'));
                    const storyLink = links.find(a => a.href && a.href.includes('medium.com') && !a.href.includes('new-story'));
                    return storyLink ? storyLink.href : window.location.href;
                })()
            ");
            return url ?? page.Url;
        }
        catch
        {
            return page.Url;
        }
    }
    
    public static async Task WaitForTextAsync(IPage page, string text, int timeoutMs = 15000, int pollIntervalMs = 100)
    {
        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                bool found = await page.EvaluateExpressionAsync<bool>($@"
                    (function() {{
                        return Array.from(document.querySelectorAll('body *'))
                            .some(el => el.textContent && el.textContent.toLowerCase().includes('{text.ToLower()}'));
                    }})()
                ");
                
                if (found)
                    return;
                
                await Task.Delay(pollIntervalMs);
            }
            
            throw new TimeoutException($"Text '{text}' not found within timeout.");
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch (Exception e)
        {
            // Ignore, can be caused by navigation
            throw new TimeoutException($"Error waiting for text '{text}': {e.Message}");
        }
    }
    
    public static async Task<bool> ClickFinalPublishButtonAsync(IPage page, int timeoutMs = 10000, int pollIntervalMs = 100)
    {
        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    bool found = await page.EvaluateExpressionAsync<bool>(@"
                        (function() {
                            var btns = Array.from(document.querySelectorAll('button[data-action=""publish""][data-testid=""publishConfirmButton""]'));
                            var btn = btns.find(b => {
                                var spans = Array.from(b.querySelectorAll('span'));
                                return spans.some(s => {
                                    var text = s.textContent.trim();
                                    return (text === 'Publish and send now' || text === 'Publish now');
                                }) && !b.disabled;
                            });
                            if (btn) { btn.click(); return true; }
                            return false;
                        })()
                    ");
                    
                    if (found)
                        return true;
                }
                catch (Exception ex)
                {
                    // If execution context was destroyed or navigation occurred, the click likely succeeded
                    if (ex.Message.Contains("Execution context", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("navigation", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("[Medium] ℹ️ Page navigated while clicking publish (button likely clicked)");
                        return true;
                    }
                }
                
                await Task.Delay(pollIntervalMs);
            }
            
            throw new TimeoutException("Final Publish button not found or not enabled within timeout.");
        }
        catch (TimeoutException)
        {
            throw;
        }
    }
    
    public static async Task ClickPublishButtonAsync(IPage page, int timeoutMs = 10000, int pollIntervalMs = 100)
    {
        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                bool found = await page.EvaluateExpressionAsync<bool>(@"
                    (function() {
                        var btns = Array.from(document.querySelectorAll('button'));
                        var btn = btns.find(b =>
                            b.textContent.trim() === 'Publish'
                            && !b.classList.contains('button--disabledPrimary')
                            && b.getAttribute('data-action') === 'show-prepublish'
                        );
                        if (btn) { btn.click(); return true; }
                        return false;
                    })()
                ");
                
                if (found)
                    return;
                
                await Task.Delay(pollIntervalMs);
            }
            
            throw new TimeoutException("Publish button not found or not enabled within timeout.");
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch (Exception e)
        {
            // Ignore can be caused by navigation
            throw new TimeoutException($"Error clicking publish button: {e.Message}");
        }
    }
    
    /// <summary>
    /// Launch options for publishing. Medium pages carry third-party content, so this uses the installed,
    /// auto-updated Chrome with its sandbox on instead of a pinned download.
    /// </summary>
    public static LaunchOptions CreateLaunchOptions(bool headless) => new LaunchOptions
    {
        Headless = headless,
        Channel = ChromeReleaseChannel.Stable,
        Args = new[] { "--window-size=1920,1080" }
    };
    
    /// <summary>
    /// Types text with realistic human-like delays between keystrokes
    /// </summary>
    private static async Task TypeRealisticallyAsync(IPage page, string text)
    {
        Random random = new Random();
        foreach (char c in text)
        {
            await page.Keyboard.TypeAsync(c.ToString());
            // Random delay between 30-120ms (human typing speed varies)
            await Task.Delay(random.Next(30, 120));
        }
    }
    
    /// <summary>
    /// Clears existing content and pastes new content realistically using clipboard
    /// </summary>
    private static async Task ClearAndPasteAsync(IPage page, string text)
    {
        Random random = new Random();
        
        // Select all with Ctrl+A
        await page.Keyboard.DownAsync("Control");
        await Task.Delay(random.Next(50, 150));
        await page.Keyboard.PressAsync("KeyA");
        await Task.Delay(random.Next(50, 150));
        await page.Keyboard.UpAsync("Control");
        await Task.Delay(random.Next(100, 200));
        
        // Delete selection
        await page.Keyboard.PressAsync("Delete");
        await Task.Delay(random.Next(100, 200));
        
        // Type the text realistically
        await TypeRealisticallyAsync(page, text);
    }
    
    /// <summary>
    /// Truncates description to 140 characters, trying to end at the last complete sentence before the limit
    /// </summary>
    private static string TruncateDescriptionTo140Chars(string description)
    {
        if (string.IsNullOrEmpty(description))
            return string.Empty;
        
        if (description.Length <= 140)
            return description;
        
        // Try to find the last sentence ending (period) before 140 characters
        int lastPeriod = description.LastIndexOf('.', Math.Min(139, description.Length - 1));
        
        if (lastPeriod > 0 && lastPeriod < 140)
        {
            // Found a period before the limit, use it
            return description.Substring(0, lastPeriod + 1);
        }
        
        // No period found or it's at position 0, just truncate at 140 and add ellipsis
        return description.Substring(0, 137) + "...";
    }
}

