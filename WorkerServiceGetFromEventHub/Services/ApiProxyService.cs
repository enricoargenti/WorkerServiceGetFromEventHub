using WorkerServiceGetFromEventHub.Models;

namespace WorkerServiceGetFromEventHub.Services;

public class ApiProxyService
{
    private readonly ILogger<ApiProxyService> _logger;
    private readonly HttpClient _client;

    public ApiProxyService(ILogger<ApiProxyService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _client = httpClientFactory.CreateClient("api");
    }

    public async Task DeleteExpiredOpenDoorRequestsAsync(int minutes)
    {
        try
        {
            string path = $"api/DoorOpenRequest/minutes/{minutes}";

            HttpResponseMessage response = await _client.DeleteAsync(path);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error on DeleteExpiredOpenDoorRequestsAsync");
            throw;
        }


    }

    public async Task InsertOpenDoorRequestAsync(OpenDoorRequest openDoorRequest)
    {
        try
        {
            string path = $"api/DoorOpenRequest";

            HttpResponseMessage response = await _client.PostAsJsonAsync(path, openDoorRequest);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error on InsertOpenDoorRequestAsync");
            throw;
        }
    }

    public async Task<OpenDoorRequest> GetDoorOpenRequestAsync(string deviceGeneratedCode)
    {
        try
        {
            string path = $"api/DoorOpenRequest/deviceGeneratedCode/{deviceGeneratedCode}";

            OpenDoorRequest openDoorRequest = null;
            HttpResponseMessage response = await _client.GetAsync(path);

            response.EnsureSuccessStatusCode();

            openDoorRequest = await response.Content.ReadAsAsync<OpenDoorRequest>();
            return openDoorRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error on GetDoorOpenRequestAsync");
            throw;
        }
    }


}
