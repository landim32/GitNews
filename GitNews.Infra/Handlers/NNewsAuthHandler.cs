using System.Net.Http.Headers;
using GitNews.DTO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAuth.ACL.Interfaces;
using NAuth.DTO.User;

namespace GitNews.Infra.Handlers;

public class NNewsAuthHandler : DelegatingHandler
{
    private readonly IUserClient _userClient;
    private readonly NNewsSettings _settings;
    private readonly NNewsTokenStore _tokenStore;
    private readonly ILogger<NNewsAuthHandler> _logger;

    public NNewsAuthHandler(
        IUserClient userClient,
        IOptions<NNewsSettings> settings,
        NNewsTokenStore tokenStore,
        ILogger<NNewsAuthHandler> logger)
    {
        _userClient = userClient;
        _settings = settings.Value;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_tokenStore.Token))
        {
            _logger.LogInformation("Authenticating with NAuth...");

            var loginParam = new LoginParam
            {
                Email = _settings.Email,
                Password = _settings.Password
            };

            var result = await _userClient.LoginWithEmailAsync(loginParam);
            if (result == null || string.IsNullOrEmpty(result.Token))
                throw new InvalidOperationException("Failed to authenticate with NAuth. Check your email and password settings.");

            _tokenStore.Token = result.Token;
            _logger.LogInformation("Successfully authenticated with NAuth");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.Token);
        return await base.SendAsync(request, cancellationToken);
    }
}
