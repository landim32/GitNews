using GitNews.DTO;
using Microsoft.Extensions.Options;

namespace GitNews.Infra.Handlers;

public class NNewsTenantHandler : DelegatingHandler
{
    private readonly NNewsSettings _settings;

    public NNewsTenantHandler(IOptions<NNewsSettings> settings)
    {
        _settings = settings.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_settings.TenantId))
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", _settings.TenantId);

        if (request.Headers.UserAgent.Count == 0)
            request.Headers.TryAddWithoutValidation("User-Agent", "GitNews/1.0");

        request.Headers.TryAddWithoutValidation("X-Device-Fingerprint", "GitNews-Server");

        return base.SendAsync(request, cancellationToken);
    }
}
