using GitNews.Infra.Interfaces.AppServices;
using Markdig;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace GitNews.Infra.AppServices;

public class LinkedInAppService : ILinkedInAppService, IAsyncDisposable
{
    private readonly ILogger<LinkedInAppService> _logger;
    private readonly IUserInteractionService _userInteraction;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public LinkedInAppService(ILogger<LinkedInAppService> logger, IUserInteractionService userInteraction)
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

        _logger.LogInformation("Navigating to LinkedIn...");
        await page.GotoAsync("https://www.linkedin.com/feed/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 30000
        });

        await page.WaitForTimeoutAsync(3000);

        var isLoggedIn = await CheckIfLoggedInAsync(page);

        if (isLoggedIn)
        {
            _logger.LogInformation("User is already logged in to LinkedIn");
            return;
        }

        _logger.LogWarning("User is NOT logged in to LinkedIn.");

        await _userInteraction.WaitForUserActionAsync(
            "You are not logged in to LinkedIn. Please log in manually in the Chrome browser window, then press Enter to continue...",
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

        throw new TimeoutException("Login timeout exceeded. Please log in to LinkedIn and try again.");
    }

    private async Task<bool> CheckIfLoggedInAsync(IPage page)
    {
        var url = page.Url;

        // LinkedIn redirects to login/authwall if not logged in
        if (url.Contains("/login") || url.Contains("/authwall") || url.Contains("/checkpoint") || url.Contains("/uas/"))
            return false;

        // If we're on the feed, we're logged in
        if (url.Contains("/feed"))
            return true;

        // Fallback: try navigating to feed and check if we get redirected
        await page.GotoAsync("https://www.linkedin.com/feed/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 15000
        });
        await page.WaitForTimeoutAsync(2000);

        var finalUrl = page.Url;
        return finalUrl.Contains("/feed") && !finalUrl.Contains("/login") && !finalUrl.Contains("/authwall");
    }

    public async Task<string> PublishArticleAsync(string title, string markdownContent, string[] tags, byte[]? coverImage = null, CancellationToken cancellationToken = default)
    {
        var page = await GetPageAsync();

        _logger.LogInformation("Navigating to LinkedIn article editor...");
        await page.GotoAsync("https://www.linkedin.com/article/new/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 30000
        });

        await page.WaitForTimeoutAsync(3000);

        // Wait for editor to load
        _logger.LogInformation("Waiting for editor to load...");
        var titleField = await WaitForTitleFieldAsync(page, cancellationToken);

        // Upload cover image if provided (before title, as LinkedIn shows it at the top)
        if (coverImage != null && coverImage.Length > 0)
        {
            _logger.LogInformation("Uploading cover image...");
            await UploadCoverImageAsync(page, coverImage);

            // Wait for cover image to finish loading in the editor before proceeding
            _logger.LogInformation("Waiting for cover image to finish loading...");
            await WaitForCoverImageLoadedAsync(page);
        }

        // Type the title — LinkedIn uses a <textarea>, not contenteditable
        _logger.LogInformation("Setting title: {Title}", title);
        await titleField.ClickAsync();
        await titleField.FillAsync(title);
        await page.WaitForTimeoutAsync(500);

        // Click on the content editor area to move focus there
        _logger.LogInformation("Typing article content...");
        await FocusEditorAsync(page);
        await page.WaitForTimeoutAsync(500);

        // Type article content
        await TypeMarkdownContentAsync(page, markdownContent);

        await page.WaitForTimeoutAsync(2000);

        // Publish the article
        _logger.LogInformation("Publishing article...");
        var articleUrl = await PublishAsync(page, title, cancellationToken);

        _logger.LogInformation("Article published successfully: {Url}", articleUrl);
        return articleUrl;
    }

    private async Task<IElementHandle> WaitForTitleFieldAsync(IPage page, CancellationToken cancellationToken = default)
    {
        // LinkedIn article editor uses a <textarea> for the title
        var selectors = new[]
        {
            "textarea#article-editor-headline__textarea",
            "textarea.article-editor-headline__textarea",
            "textarea[placeholder='Título']",
            "textarea[placeholder='Title']",
            ".article-editor-headline textarea"
        };

        var result = await TryFindElementAsync(page, selectors, maxAttempts: 10);
        if (result != null)
            return result;

        // Editor not found — likely a verification or interstitial page
        _logger.LogWarning("Could not find the editor title field. LinkedIn may be showing a verification page.");

        await _userInteraction.WaitForUserActionAsync(
            "LinkedIn is showing a verification page or the editor did not load. Please complete any verification in the Chrome browser window, then press Enter to continue...",
            cancellationToken);

        _logger.LogInformation("Retrying to find the editor after user interaction...");
        await page.GotoAsync("https://www.linkedin.com/article/new/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 30000
        });
        await page.WaitForTimeoutAsync(3000);

        result = await TryFindElementAsync(page, selectors, maxAttempts: 10);
        if (result != null)
            return result;

        throw new InvalidOperationException("Could not find the title field in LinkedIn editor after verification. The editor layout may have changed.");
    }

    private async Task<IElementHandle?> TryFindElementAsync(IPage page, string[] selectors, int maxAttempts)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            foreach (var selector in selectors)
            {
                try
                {
                    var element = await page.QuerySelectorAsync(selector);
                    if (element != null && await element.IsVisibleAsync())
                        return element;
                }
                catch (PlaywrightException)
                {
                    // Context may have been destroyed
                }
            }
            await page.WaitForTimeoutAsync(1000);
        }

        return null;
    }

    private async Task UploadCoverImageAsync(IPage page, byte[] imageBytes)
    {
        try
        {
            // LinkedIn has a "Carregar do computador" / "Upload from computer" button
            // inside div.article-editor-cover-media__placeholder
            var uploadButtonSelectors = new[]
            {
                "div.article-editor-cover-media__placeholder button",
                "button[aria-label='Carregar do computador']",
                "button[aria-label='Upload from computer']",
                ".article-editor-cover-media__placeholder-upload-buttons button"
            };

            IElementHandle? uploadButton = null;
            foreach (var selector in uploadButtonSelectors)
            {
                uploadButton = await page.QuerySelectorAsync(selector);
                if (uploadButton != null && await uploadButton.IsVisibleAsync())
                    break;
                uploadButton = null;
            }

            if (uploadButton == null)
            {
                _logger.LogWarning("Could not find the cover image upload button. Continuing without image");
                return;
            }

            // Save image to temp file
            var tempPath = Path.Combine(Path.GetTempPath(), $"linkedin-cover-{Guid.NewGuid()}.png");
            await File.WriteAllBytesAsync(tempPath, imageBytes);

            try
            {
                // LinkedIn opens a file chooser when the button is clicked
                var fileChooserTask = page.WaitForFileChooserAsync(new PageWaitForFileChooserOptions { Timeout = 5000 });
                await uploadButton.ClickAsync();
                var fileChooser = await fileChooserTask;
                await fileChooser.SetFilesAsync(tempPath);

                _logger.LogInformation("Cover image uploaded, waiting for processing...");
                await page.WaitForTimeoutAsync(5000);

                // LinkedIn shows a crop/preview dialog after image upload — click "Avançar" / "Next" to confirm
                await ClickCoverImageConfirmButtonAsync(page);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("File chooser did not appear. Trying file input fallback...");

                // Fallback: look for a hidden file input
                var fileInput = await page.QuerySelectorAsync("input[type='file']");
                if (fileInput != null)
                {
                    await fileInput.SetInputFilesAsync(tempPath);
                    _logger.LogInformation("Cover image uploaded via file input, waiting for processing...");
                    await page.WaitForTimeoutAsync(5000);

                    // LinkedIn shows a crop/preview dialog after image upload — click "Avançar" / "Next" to confirm
                    await ClickCoverImageConfirmButtonAsync(page);
                }
                else
                {
                    _logger.LogWarning("No file input found. Continuing without cover image");
                }
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

    private async Task WaitForCoverImageLoadedAsync(IPage page)
    {
        // Wait for the cover image <img> to appear and be fully loaded
        for (int attempt = 0; attempt < 15; attempt++)
        {
            var loaded = await page.EvaluateAsync<bool>(@"() => {
                const img = document.querySelector('.article-editor-cover-media__image img, .article-editor-cover-image img, img[data-test-article-cover-image]');
                return img != null && img.complete && img.naturalWidth > 0;
            }");

            if (loaded)
            {
                _logger.LogInformation("Cover image loaded successfully");
                return;
            }

            await page.WaitForTimeoutAsync(1000);
        }

        // Fallback: just wait a fixed amount if we couldn't detect the image
        _logger.LogWarning("Could not confirm cover image loaded, waiting extra time...");
        await page.WaitForTimeoutAsync(5000);
    }

    private async Task ClickCoverImageConfirmButtonAsync(IPage page)
    {
        var confirmSelectors = new[]
        {
            "button.share-box-footer__primary-btn",
            "button[aria-label='Avançar']",
            "button[aria-label='Next']",
            "button:has-text('Avançar')",
            "button:has-text('Next')"
        };

        var button = await FindVisibleButtonAsync(page, confirmSelectors);
        if (button != null)
        {
            var text = (await button.TextContentAsync())?.Trim();
            _logger.LogInformation("Clicking cover image confirm button: {Text}", text);
            await button.ClickAsync();
            await page.WaitForTimeoutAsync(3000);
        }
        else
        {
            _logger.LogWarning("Could not find the cover image confirm button. The dialog may have closed automatically");
        }
    }

    private async Task TypeMarkdownContentAsync(IPage page, string markdownContent)
    {
        var segments = SplitMarkdownSegments(markdownContent);

        _logger.LogInformation("Split content into {Count} segments for LinkedIn editor", segments.Count);

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

        _logger.LogInformation("Article content inserted into LinkedIn editor");
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

        await FocusEditorAsync(page);
        await page.Keyboard.PressAsync("Control+v");
        await page.WaitForTimeoutAsync(1000);
    }

    private async Task<IElementHandle?> FindCodeBlockToolbarButtonAsync(IPage page)
    {
        // The button uses scaffold-formatted-text-editor-icon-button class
        // and contains an SVG with data-test-icon="curly-braces-medium"
        var selectors = new[]
        {
            "button.scaffold-formatted-text-editor-icon-button svg[data-test-icon='curly-braces-medium']",
            "button svg[data-test-icon='curly-braces-medium']"
        };

        foreach (var selector in selectors)
        {
            var svg = await page.QuerySelectorAsync(selector);
            if (svg != null)
            {
                var button = await svg.EvaluateHandleAsync("el => el.closest('button')") as IElementHandle;
                if (button != null && await button.IsVisibleAsync())
                    return button;
            }
        }

        return null;
    }

    private async Task TypeCodeBlockAsync(IPage page, string codeContent)
    {
        await FocusEditorAsync(page);

        // Ensure we're on a new line before activating code block
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(300);

        // LinkedIn's ProseMirror editor uses a toolbar button (curly-braces icon) for "Código Ativo"
        var codeButton = await FindCodeBlockToolbarButtonAsync(page);

        if (codeButton != null)
        {
            _logger.LogInformation("Clicking code block toolbar button...");
            await codeButton.ClickAsync();
            await page.WaitForTimeoutAsync(1000);

            // After clicking, the editor should create a <pre> / code block element.
            // Ensure the cursor is inside it before typing.
            await page.EvaluateAsync(@"() => {
                const pre = document.querySelector('div.ProseMirror pre:last-of-type')
                    || document.querySelector('[data-test-article-editor-content-textbox] pre:last-of-type');
                if (pre) {
                    const code = pre.querySelector('code') || pre;
                    code.focus();
                    const range = document.createRange();
                    range.selectNodeContents(code);
                    range.collapse(false);
                    const sel = window.getSelection();
                    sel.removeAllRanges();
                    sel.addRange(range);
                }
            }");
            await page.WaitForTimeoutAsync(300);
        }
        else
        {
            _logger.LogWarning("Could not find code block toolbar button. Typing code as plain text");
        }

        // Type each line of code inside the code block
        var lines = codeContent.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            await page.Keyboard.TypeAsync(lines[i], new KeyboardTypeOptions { Delay = 5 });

            if (i < lines.Length - 1)
                await page.Keyboard.PressAsync("Enter");
        }

        await page.WaitForTimeoutAsync(500);

        // Exit code block: toggle off the button if it's still active
        codeButton = await FindCodeBlockToolbarButtonAsync(page);
        if (codeButton != null)
        {
            var isPressed = await codeButton.GetAttributeAsync("aria-pressed");
            if (isPressed == "true")
            {
                _logger.LogInformation("Toggling off code block mode...");
                await codeButton.ClickAsync();
                await page.WaitForTimeoutAsync(500);
            }
        }

        // Move cursor after the code block
        await page.Keyboard.PressAsync("ArrowDown");
        await page.WaitForTimeoutAsync(300);
    }

    private async Task FocusEditorAsync(IPage page)
    {
        // LinkedIn article editor: div.ProseMirror with data-test-article-editor-content-textbox
        await page.EvaluateAsync(@"() => {
            const editor = document.querySelector('div.ProseMirror[data-test-article-editor-content-textbox]')
                || document.querySelector('div.ProseMirror[contenteditable=""true""]')
                || document.querySelector('[data-test-article-editor-content-textbox]')
                || document.querySelector('[role=""textbox""][contenteditable=""true""]')
                || document.querySelector('[contenteditable=""true""]');
            if (editor) {
                editor.focus();
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

    private async Task<string> PublishAsync(IPage page, string title, CancellationToken cancellationToken)
    {
        // LinkedIn article editor has "Avançar" (Next) button with class article-editor-nav__publish
        _logger.LogInformation("Looking for the Next/Publish button...");
        await ClickButtonAsync(page, new[]
        {
            "button.article-editor-nav__publish",
            "button:has-text('Avançar')",
            "button:has-text('Next')",
            "button:has-text('Publish')"
        }, "Next/Publish");

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await page.WaitForTimeoutAsync(3000);

        // LinkedIn shows a publish dialog with a topic textarea
        // "Conte para sua rede qual é o tópico do seu artigo..."
        _logger.LogInformation("Filling article topic field with title...");
        var topicFieldSelectors = new[]
        {
            ".share-creation-state__text-editor .ql-editor",
            "[data-test-share-creation-text-editor] .ql-editor",
            ".share-box__text-editor .ql-editor",
            "div.ql-editor[contenteditable='true']"
        };

        var topicField = await TryFindElementAsync(page, topicFieldSelectors, maxAttempts: 5);
        if (topicField != null)
        {
            await topicField.ClickAsync();
            await page.WaitForTimeoutAsync(300);
            await page.Keyboard.TypeAsync(title, new KeyboardTypeOptions { Delay = 10 });
            await page.WaitForTimeoutAsync(500);
            _logger.LogInformation("Topic field filled with article title");
        }
        else
        {
            _logger.LogWarning("Could not find the topic text field in publish dialog");
        }

        // Click the "Publicar" / "Publish" button (share-actions__primary-action)
        _logger.LogInformation("Looking for the confirmation Publish button...");
        var confirmButton = await FindVisibleButtonAsync(page, new[]
        {
            "button.share-actions__primary-action",
            "button.share-actions__primary-action:has-text('Publicar')",
            "button.share-actions__primary-action:has-text('Publish')",
            "button:has-text('Publicar')",
            "button:has-text('Publish')"
        });

        if (confirmButton != null)
        {
            var text = (await confirmButton.TextContentAsync())?.Trim();
            _logger.LogInformation("Clicking confirmation button: {Text}", text);
            await confirmButton.ClickAsync();
        }
        else
        {
            _logger.LogWarning("Could not find the confirmation Publish button");
        }

        _logger.LogInformation("Waiting for article to be published...");
        await page.WaitForTimeoutAsync(5000);

        var articleUrl = page.Url;

        // Wait for redirect to the published article
        if (articleUrl.Contains("/article/new") || articleUrl.Contains("/article/edit"))
        {
            try
            {
                await page.WaitForURLAsync(
                    url => !url.Contains("/article/new") && !url.Contains("/article/edit"),
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

    private async Task ClickButtonAsync(IPage page, string[] selectors, string buttonName)
    {
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
                    // Context destroyed, retry
                }
            }

            await page.WaitForTimeoutAsync(1000);
        }

        throw new InvalidOperationException($"Could not find the '{buttonName}' button on LinkedIn editor.");
    }

    private async Task<IElementHandle?> FindVisibleButtonAsync(IPage page, string[] selectors)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            foreach (var selector in selectors)
            {
                try
                {
                    var buttons = await page.QuerySelectorAllAsync(selector);
                    foreach (var btn in buttons)
                    {
                        if (await btn.IsVisibleAsync())
                            return btn;
                    }
                }
                catch (PlaywrightException)
                {
                    // Context destroyed, retry
                }
            }

            await page.WaitForTimeoutAsync(1000);
        }

        return null;
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

        if (inCodeBlock && currentCode.Length > 0)
        {
            segments.Add(new ContentSegment(currentCode.ToString().TrimEnd(), true));
        }
        else if (currentText.Length > 0)
        {
            segments.Add(new ContentSegment(currentText.ToString().TrimEnd(), false));
        }

        return segments;
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
