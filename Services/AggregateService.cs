using Aggregator.Models;
using System.Text;
using System.Text.Json;

namespace Aggregator.Services
{
    public class AggregateService
    {
        private string _serverUrl;
        private string _username;
        private string _password;

        private HttpClient _client = new HttpClient(new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
        })
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        private DateTime _lastRefresh = DateTime.MinValue;

        public AggregateService(string serverUrl, string username, string password)
        {
            _serverUrl = serverUrl;
            _username = username;
            _password = password;
        }

        public async Task<List<string>> GetMounts()
        {
            var response = await GetVariable("plugins.com_tibbo_linkserver_plugin_context_distributed", "providers");

            var mountNamesList = new List<string>();
            var mounts = JsonSerializer.Deserialize<List<Mount>>(response);

            if (mounts != null)
            {
                foreach (var mount in mounts)
                {
                    mountNamesList.Add(mount.name);
                }
            }

            return mountNamesList;
        }

        public async Task<string> DoFunction(string context, string functionName, object? content = null)
        {
            if (content == null)
            {
                content = new int[0];
            }

            if (content is not string)
            {
                content = JsonSerializer.Serialize(content);
            }

            var response = await DoAction($"contexts/{context}/functions/{functionName}", "POST", content);
            return response;
        }

        public async Task<string> GetVariable(string context, string variableName)
        {
            var response = await DoAction($"contexts/{context}/variables/{variableName}", "GET");
            return response;
        }

        public async Task<string> DoAction(string actionUrl, string method, object? content = null)
        {

            await CheckAuthorize();

            Console.WriteLine($"Do {actionUrl} ({method}): {content}");


            if (content is not string)
            {
                content = JsonSerializer.Serialize(content);
            }

            StringContent body = new StringContent(content as string, Encoding.UTF8, "application/json");

            HttpResponseMessage? response = null;

            switch (method)
            {
                case "POST":
                    response = await _client.PostAsync($"{_serverUrl}:8443/rest/v1/{actionUrl}", body);
                    break;

                case "GET":
                    response = await _client.GetAsync($"{_serverUrl}:8443/rest/v1/{actionUrl}");
                    break;

                case "PUT":
                    response = await _client.PutAsync($"{_serverUrl}:8443/rest/v1/{actionUrl}", body);
                    break;

                case "PATCH":
                    response = await _client.PatchAsync($"{_serverUrl}:8443/rest/v1/{actionUrl}", body);
                    break;

                case "DELETE":
                    response = await _client.DeleteAsync($"{_serverUrl}:8443/rest/v1/{actionUrl}");
                    break;

                default:
                    throw new NotImplementedException($"Method {method} not implemented");
            }

            if (response != null && response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Ok {actionUrl} ({method})");

                return result;
            }
            else
            {
                throw new HttpRequestException($"Cant do action {method} {actionUrl}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            }

        }

        public async Task<string> Evaluate(string expression, string? defaultTable = null, string defaultContext = "")
        {
            var data = new { expression, defaultTable, defaultContext };
            var result = await DoAction("evaluate", "POST", data);
            return result;

        }

        private async Task CheckAuthorize()
        {
            Console.WriteLine("Check Authorize...");
            if (!_client.DefaultRequestHeaders.Contains("Authorization") || _lastRefresh < DateTime.Now.AddMinutes(-30))
            {
                await Authorize();
            }
            Console.WriteLine("Authorization OK");
        }

        private async Task Authorize()
        {
            Console.WriteLine("Authorization...");
            var authData = new { username = _username, password = _password };
            var json = JsonSerializer.Serialize(authData);
            var body = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync($"{_serverUrl}:8443/rest/auth", body);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();

                var authResponse = JsonSerializer.Deserialize<AuthResponse>(result);
                if (authResponse?.token != null)
                {
                    _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authResponse.token}");
                    _lastRefresh = DateTime.Now;
                }
                else
                {
                    throw new HttpRequestException("Auth token is null");
                }
            }
            else
            {
                throw new HttpRequestException("Cant authorize: " + response.ToString());
            }
        }
    }
}