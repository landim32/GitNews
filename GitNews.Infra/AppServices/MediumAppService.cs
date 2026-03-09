using GitNews.Infra.Interfaces.AppServices;
using Markdig;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace GitNews.Infra.AppServices;

public class MediumAppService : IMediumAppService, IAsyncDisposable
{
    private readonly ILogger<MediumAppService> _logger;
    private readonly IUserInteractionService _userInteraction;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public MediumAppService(ILogger<MediumAppService> logger, IUserInteractionService userInteraction)
    {
        _logger = logger;
        _userInteraction = userInteraction;
    }

    private async Task<IPage> GetPageAsync()
    {
        if (_page != null)
            return _page;

        _playwright = await Playwright.CreateAsync();

        _logger.LogInformation("Connecting to Chrome via CDP on 127.0.0.1:9222...");
        _browser = await _playwright.Chromium.ConnectOverCDPAsync("http://127.0.0.1:9222");

        var contexts = _browser.Contexts;
        var context = contexts.Count > 0 ? contexts[0] : await _browser.NewContextAsync();
        _page = await context.NewPageAsync();

        return _page;
    }

    public async Task EnsureLoggedInAsync(CancellationToken cancellationToken = default)
    {
        var page = await GetPageAsync();

        _logger.LogInformation("Navigating to Medium...");
        await page.GotoAsync("https://medium.com/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 30000
        });

        await page.WaitForTimeoutAsync(3000);

        var isLoggedIn = await CheckIfLoggedInAsync(page);

        if (isLoggedIn)
        {
            _logger.LogInformation("User is already logged in to Medium");
            return;
        }

        _logger.LogWarning("User is NOT logged in to Medium.");

        // Notify user — in Console this prompts, in Worker this throws
        await _userInteraction.WaitForUserActionAsync(
            "You are not logged in to Medium. Please log in manually in the Chrome browser window, then press Enter to continue...",
            cancellationToken);

        _logger.LogInformation("Checking login status after user interaction...");
        await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForTimeoutAsync(2000);

        if (await CheckIfLoggedInAsync(page))
        {
            _logger.LogInformation("Login detected! Proceeding...");
            return;
        }

        _logger.LogWarning("Still not logged in. Waiting for login... (checking every 10 seconds, timeout: 5 minutes)");

        var timeout = TimeSpan.FromMinutes(5);
        var elapsed = TimeSpan.Zero;
        var interval = TimeSpan.FromSeconds(10);

        while (elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(interval, cancellationToken);
            elapsed += interval;

            await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForTimeoutAsync(2000);

            if (await CheckIfLoggedInAsync(page))
            {
                _logger.LogInformation("Login detected! Proceeding...");
                return;
            }

            _logger.LogInformation("Still waiting for login... ({Elapsed}s / {Timeout}s)",
                (int)elapsed.TotalSeconds, (int)timeout.TotalSeconds);
        }

        throw new TimeoutException("Login timeout exceeded. Please log in to Medium and try again.");
    }

    private async Task<bool> CheckIfLoggedInAsync(IPage page)
    {
        // Medium shows avatar/user menu when logged in; shows "Sign in" or "Get started" when not
        var signInButton = await page.QuerySelectorAsync("a[href*='signin'], a[href*='login'], button:has-text('Sign in'), button:has-text('Get started')");
        if (signInButton != null && await signInButton.IsVisibleAsync())
            return false;

        // Check for user avatar or profile indicators
        var avatarOrMenu = await page.QuerySelectorAsync("img[alt*='avatar' i], button[aria-label*='user' i], img[class*='avatar' i], div[data-testid='headerAvatar']");
        if (avatarOrMenu != null)
            return true;

        // Fallback: check if the "Write" button is visible (only appears when logged in)
        var writeButton = await page.QuerySelectorAsync("[data-testid='headerWriteButton'], a[href*='/new-story'], a:has-text('Write')");
        if (writeButton != null && await writeButton.IsVisibleAsync())
            return true;

        return false;
    }

    public async Task<string> PublishArticleAsync(string title, string markdownContent, string[] tags, byte[]? coverImage = null, CancellationToken cancellationToken = default)
    {
        var page = await GetPageAsync();

        _logger.LogInformation("Navigating to Medium homepage...");
        await page.GotoAsync("https://medium.com/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 30000
        });

        await page.WaitForTimeoutAsync(3000);

        _logger.LogInformation("Clicking 'Write' button...");
        await ClickWriteButtonAsync(page, cancellationToken);

        // Wait for editor to load
        _logger.LogInformation("Waiting for editor to load...");
        var titleField = await WaitForTitleFieldAsync(page, cancellationToken);

        // Type the title
        _logger.LogInformation("Setting title: {Title}", title);
        await titleField.ClickAsync();
        await titleField.FillAsync(title);
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(1000);

        // Upload cover image if provided
        if (coverImage != null && coverImage.Length > 0)
        {
            _logger.LogInformation("Uploading cover image...");
            await UploadCoverImageAsync(page, coverImage);
        }

        // Type content paragraph by paragraph
        _logger.LogInformation("Typing article content...");
        await TypeMarkdownContentAsync(page, markdownContent);

        await page.WaitForTimeoutAsync(2000);

        // Publish the article
        _logger.LogInformation("Publishing article...");
        var articleUrl = await PublishAsync(page, tags, cancellationToken);

        _logger.LogInformation("Article published successfully: {Url}", articleUrl);
        return articleUrl;
    }

    private async Task<IElementHandle> WaitForTitleFieldAsync(IPage page, CancellationToken cancellationToken = default)
    {
        // Medium's editor uses a contenteditable title field
        var selectors = new[]
        {
            "h3[data-contents='true']",
            "h3.graf--title",
            "[data-testid='post-title']",
            "div[role='textbox'] h3",
            "section h3[contenteditable]",
            "h3[contenteditable='true']",
            "div.section-inner h3",
            "[placeholder*='Title']",
            "p[data-placeholder='Title']"
        };

        // First attempt: try to find the title field normally
        var result = await TryFindTitleFieldAsync(page, selectors, maxAttempts: 10);
        if (result != null)
            return result;

        // Editor not found — likely a human verification page (captcha, etc.)
        _logger.LogWarning("Could not find the editor title field. Medium may be showing a human verification page.");

        // This will prompt the user in Console or throw in Worker
        await _userInteraction.WaitForUserActionAsync(
            "Medium is showing a verification page. Please complete the verification in the Chrome browser window, then press Enter to continue...",
            cancellationToken);

        // After user interaction, reload and retry
        _logger.LogInformation("Retrying to find the editor after user interaction...");
        await page.GotoAsync("https://medium.com/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 30000
        });
        await page.WaitForTimeoutAsync(3000);
        await ClickWriteButtonAsync(page, cancellationToken);

        result = await TryFindTitleFieldAsync(page, selectors, maxAttempts: 10);
        if (result != null)
            return result;

        throw new InvalidOperationException("Could not find the title field in Medium editor after verification. The editor layout may have changed.");
    }

    private async Task<IElementHandle?> TryFindTitleFieldAsync(IPage page, string[] selectors, int maxAttempts)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            foreach (var selector in selectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null && await element.IsVisibleAsync())
                    return element;
            }
            await page.WaitForTimeoutAsync(1000);
        }

        // Fallback: try finding any contenteditable element that looks like a title
        var fallback = await page.QuerySelectorAsync("[contenteditable='true']");
        return fallback;
    }

    private async Task ClickWriteButtonAsync(IPage page, CancellationToken cancellationToken = default)
    {
        var writeSelector = "[data-testid='headerWriteButton'], a[href*='/new-story'], a:has-text('Write')";

        try
        {
            await page.WaitForSelectorAsync(writeSelector, new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 15000
            });

            await page.ClickAsync(writeSelector);
            _logger.LogInformation("Clicked 'Write' button");
            await page.WaitForTimeoutAsync(3000);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Could not find the 'Write' button on Medium.");

            await _userInteraction.WaitForUserActionAsync(
                "Could not find the 'Write' button. Please open the Medium editor manually in the Chrome browser window, then press Enter to continue...",
                cancellationToken);

            _logger.LogInformation("User opened the editor manually, proceeding...");
        }
    }

    private async Task UploadCoverImageAsync(IPage page, byte[] imageBytes)
    {
        try
        {
            var base64 = Convert.ToBase64String(imageBytes);

            // Paste image via clipboard DataTransfer with a Blob built from base64
            await page.EvaluateAsync(@"(base64) => {
                const byteCharacters = atob(base64);
                const byteNumbers = new Uint8Array(byteCharacters.length);
                for (let i = 0; i < byteCharacters.length; i++) {
                    byteNumbers[i] = byteCharacters.charCodeAt(i);
                }
                const blob = new Blob([byteNumbers], { type: 'image/png' });
                const file = new File([blob], 'cover.png', { type: 'image/png' });
                const dataTransfer = new DataTransfer();
                dataTransfer.items.add(file);
                const pasteEvent = new ClipboardEvent('paste', {
                    clipboardData: dataTransfer,
                    bubbles: true,
                    cancelable: true
                });
                document.activeElement.dispatchEvent(pasteEvent);
            }", base64);

            _logger.LogInformation("Cover image pasted, waiting for processing...");
            await page.WaitForTimeoutAsync(5000);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upload cover image via clipboard paste. Trying file input fallback...");
            await UploadCoverImageViaFileInputAsync(page, imageBytes);
        }
    }

    private async Task UploadCoverImageViaFileInputAsync(IPage page, byte[] imageBytes)
    {
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"medium-cover-{Guid.NewGuid()}.png");
            await File.WriteAllBytesAsync(tempPath, imageBytes);

            try
            {
                // Look for any file input on the page (Medium may have a hidden one)
                var fileInput = await page.QuerySelectorAsync("input[type='file']");

                if (fileInput != null)
                {
                    await fileInput.SetInputFilesAsync(tempPath);
                    _logger.LogInformation("Cover image uploaded via file input, waiting for processing...");
                    await page.WaitForTimeoutAsync(5000);
                    return;
                }

                // Try the + button to reveal the file input
                var addButton = await page.QuerySelectorAsync("button[data-action='inline-menu-image'], button[title='Add an image'], button:has-text('+'), [data-testid='imageButton']");
                if (addButton != null && await addButton.IsVisibleAsync())
                {
                    await addButton.ClickAsync();
                    await page.WaitForTimeoutAsync(1000);

                    fileInput = await page.QuerySelectorAsync("input[type='file']");
                    if (fileInput != null)
                    {
                        await fileInput.SetInputFilesAsync(tempPath);
                        _logger.LogInformation("Cover image uploaded via file input, waiting for processing...");
                        await page.WaitForTimeoutAsync(5000);
                        return;
                    }
                }

                _logger.LogWarning("Could not find file input for image upload. Continuing without image");
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upload cover image. Continuing without image");
        }
    }

    private async Task TypeMarkdownContentAsync(IPage page, string markdownContent)
    {
        // Split markdown into segments: regular content and code blocks.
        // Regular content is pasted as HTML (Medium interprets headings, bold, etc.).
        // Code blocks must be typed using Medium's ``` trigger since pasting <pre><code>
        // does not create native Medium code blocks.
        var segments = SplitMarkdownSegments(markdownContent);

        _logger.LogInformation("Split content into {Count} segments for Medium editor", segments.Count);

        foreach (var segment in segments)
        {
            if (segment.IsCodeBlock)
            {
                await TypeCodeBlockAsync(page, segment.Content);
            }
            else
            {
                await PasteHtmlSegmentAsync(page, segment.Content);
            }
        }

        _logger.LogInformation("Article content inserted into Medium editor");
    }

    private async Task PasteHtmlSegmentAsync(IPage page, string markdownSegment)
    {
        if (string.IsNullOrWhiteSpace(markdownSegment))
            return;

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        var html = Markdown.ToHtml(markdownSegment, pipeline);

        // Copy HTML to clipboard via off-screen contenteditable element
        await page.EvaluateAsync(@"(html) => {
            const container = document.createElement('div');
            container.innerHTML = html;
            container.style.position = 'fixed';
            container.style.left = '-9999px';
            container.style.top = '0';
            container.setAttribute('contenteditable', 'true');
            document.body.appendChild(container);

            const range = document.createRange();
            range.selectNodeContents(container);
            const selection = window.getSelection();
            selection.removeAllRanges();
            selection.addRange(range);

            document.execCommand('copy');

            selection.removeAllRanges();
            document.body.removeChild(container);
        }", html);

        await page.WaitForTimeoutAsync(300);

        // Focus editor and paste
        await FocusEditorAsync(page);
        await page.Keyboard.PressAsync("Control+v");
        await page.WaitForTimeoutAsync(1000);
    }

    private async Task TypeCodeBlockAsync(IPage page, string codeContent)
    {
        await FocusEditorAsync(page);

        // Type ``` to trigger Medium's native code block
        await page.Keyboard.TypeAsync("```", new KeyboardTypeOptions { Delay = 50 });
        await page.WaitForTimeoutAsync(1000);

        // Type each line of code
        var lines = codeContent.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            await page.Keyboard.TypeAsync(lines[i], new KeyboardTypeOptions { Delay = 5 });

            // Press Enter for all lines except the last
            if (i < lines.Length - 1)
                await page.Keyboard.PressAsync("Enter");
        }

        // Press Enter twice to exit the code block
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(500);
    }

    private async Task FocusEditorAsync(IPage page)
    {
        await page.EvaluateAsync(@"() => {
            const editor = document.querySelector('[role=""textbox""][contenteditable=""true""]')
                || document.querySelector('div[contenteditable=""true""]')
                || document.querySelector('[contenteditable=""true""]');
            if (editor) {
                editor.focus();
                // Move cursor to the end
                const selection = window.getSelection();
                if (selection && editor.lastChild) {
                    const range = document.createRange();
                    range.selectNodeContents(editor);
                    range.collapse(false);
                    selection.removeAllRanges();
                    selection.addRange(range);
                }
            }
        }");
        await page.WaitForTimeoutAsync(200);
    }

    private record ContentSegment(string Content, bool IsCodeBlock);

    private static List<ContentSegment> SplitMarkdownSegments(string markdown)
    {
        var segments = new List<ContentSegment>();
        var lines = markdown.Split('\n');
        var currentText = new System.Text.StringBuilder();
        var currentCode = new System.Text.StringBuilder();
        var inCodeBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();

            if (trimmed.StartsWith("```"))
            {
                if (!inCodeBlock)
                {
                    // Flush accumulated text
                    if (currentText.Length > 0)
                    {
                        segments.Add(new ContentSegment(currentText.ToString().TrimEnd(), false));
                        currentText.Clear();
                    }

                    inCodeBlock = true;
                    currentCode.Clear();
                }
                else
                {
                    // End of code block — flush code
                    segments.Add(new ContentSegment(currentCode.ToString().TrimEnd(), true));
                    currentCode.Clear();
                    inCodeBlock = false;
                }

                continue;
            }

            if (inCodeBlock)
            {
                if (currentCode.Length > 0)
                    currentCode.AppendLine();
                currentCode.Append(line);
            }
            else
            {
                currentText.AppendLine(line);
            }
        }

        // Flush remaining
        if (inCodeBlock && currentCode.Length > 0)
        {
            // Unclosed code block — treat as code anyway
            segments.Add(new ContentSegment(currentCode.ToString().TrimEnd(), true));
        }
        else if (currentText.Length > 0)
        {
            segments.Add(new ContentSegment(currentText.ToString().TrimEnd(), false));
        }

        return segments;
    }

    private async Task<string> PublishAsync(IPage page, string[] tags, CancellationToken cancellationToken)
    {
        // Click the "Publish" button in the top bar
        _logger.LogInformation("Looking for the Publish button...");
        await ClickPublishButtonAsync(page);

        // Wait for the publish dialog/panel to stabilize after navigation
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await page.WaitForTimeoutAsync(3000);

        // Add tags in the publish dialog
        if (tags.Length > 0)
        {
            _logger.LogInformation("Adding tags: {Tags}", string.Join(", ", tags));
            await AddTagsAsync(page, tags.Take(5).ToArray());
        }

        // Click the final "Publish now" button
        _logger.LogInformation("Looking for the 'Publish now' button...");
        await ClickPublishNowButtonAsync(page);

        _logger.LogInformation("Clicked 'Publish now'. Waiting for redirect...");

        // Wait for navigation to the published article
        await page.WaitForTimeoutAsync(5000);

        var articleUrl = page.Url;

        // If still on editor, wait longer
        if (articleUrl.Contains("new-story") || articleUrl.Contains("edit"))
        {
            try
            {
                await page.WaitForURLAsync(url => !url.Contains("new-story") && !url.Contains("edit"),
                    new PageWaitForURLOptions { Timeout = 30000 });
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timed out waiting for redirect after publish");
            }

            articleUrl = page.Url;
        }

        return articleUrl;
    }

    private async Task ClickPublishButtonAsync(IPage page)
    {
        var selectors = new[]
        {
            "button:has-text('Publish')",
            "[data-testid='publishButton']",
            "button[data-action='show-prepublish']",
            "header button:has-text('Publish')",
            "nav button:has-text('Publish')"
        };

        for (int attempt = 0; attempt < 5; attempt++)
        {
            foreach (var selector in selectors)
            {
                try
                {
                    var btn = await page.QuerySelectorAsync(selector);
                    if (btn != null && await btn.IsVisibleAsync())
                    {
                        await btn.ClickAsync();
                        return;
                    }
                }
                catch (PlaywrightException)
                {
                    // Context destroyed during query, wait and retry
                }
            }

            await page.WaitForTimeoutAsync(1000);
        }

        throw new InvalidOperationException("Could not find the Publish button on Medium editor.");
    }

    private async Task AddTagsAsync(IPage page, string[] tags)
    {
        try
        {
            // Wait for the publish dialog to be fully loaded
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForTimeoutAsync(1000);

            var tagInputSelectors = new[]
            {
                "input[placeholder*='tag' i]",
                "input[placeholder*='Add a topic' i]",
                "input[data-testid='tagInput']",
                "input[type='text']"
            };

            IElementHandle? tagInput = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                foreach (var selector in tagInputSelectors)
                {
                    try
                    {
                        tagInput = await page.QuerySelectorAsync(selector);
                        if (tagInput != null && await tagInput.IsVisibleAsync())
                            break;
                        tagInput = null;
                    }
                    catch (PlaywrightException)
                    {
                        tagInput = null;
                    }
                }

                if (tagInput != null)
                    break;

                await page.WaitForTimeoutAsync(1000);
            }

            if (tagInput == null)
            {
                _logger.LogWarning("Could not find tag input field. Skipping tags");
                return;
            }

            foreach (var tag in tags)
            {
                try
                {
                    await tagInput.FillAsync(tag);
                    await page.WaitForTimeoutAsync(1000);

                    // Try to select the first suggestion
                    var suggestion = await page.QuerySelectorAsync(
                        "li[role='option'], div[class*='suggestion'], button:has-text('" + tag.Replace("'", "\\'") + "')");
                    if (suggestion != null)
                    {
                        await suggestion.ClickAsync();
                    }
                    else
                    {
                        await page.Keyboard.PressAsync("Enter");
                    }

                    await page.WaitForTimeoutAsync(500);
                }
                catch (PlaywrightException ex)
                {
                    _logger.LogWarning(ex, "Failed to add tag '{Tag}'. Continuing with remaining tags", tag);

                    // Re-query the tag input as it may have been destroyed
                    tagInput = null;
                    foreach (var selector in tagInputSelectors)
                    {
                        try
                        {
                            tagInput = await page.QuerySelectorAsync(selector);
                            if (tagInput != null && await tagInput.IsVisibleAsync())
                                break;
                            tagInput = null;
                        }
                        catch (PlaywrightException)
                        {
                            tagInput = null;
                        }
                    }

                    if (tagInput == null)
                    {
                        _logger.LogWarning("Tag input lost after error. Stopping tag addition");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add tags. Article will be published without tags");
        }
    }

    private async Task ClickPublishNowButtonAsync(IPage page)
    {
        var selectors = new[]
        {
            "button:has-text('Publish now')",
            "[data-testid='publishNowButton']",
            "button[data-action='publish']",
            "button:has-text('Publish')"
        };

        for (int attempt = 0; attempt < 10; attempt++)
        {
            foreach (var selector in selectors)
            {
                try
                {
                    var buttons = await page.QuerySelectorAllAsync(selector);
                    foreach (var btn in buttons)
                    {
                        if (await btn.IsVisibleAsync())
                        {
                            var text = (await btn.TextContentAsync())?.Trim();
                            // Prefer "Publish now" over just "Publish" to avoid re-clicking the initial button
                            if (text?.Contains("now", StringComparison.OrdinalIgnoreCase) == true ||
                                text?.Equals("Publish", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                await btn.ClickAsync();
                                return;
                            }
                        }
                    }
                }
                catch (PlaywrightException)
                {
                    // Context may have been destroyed, wait and retry
                }
            }

            await page.WaitForTimeoutAsync(1000);
        }

        throw new InvalidOperationException("Could not find the 'Publish now' confirmation button.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
    }
}
