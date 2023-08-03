using Newtonsoft.Json;

namespace BasicMarketoAuth
{

    public class MainClass
    {
        public static void Main(string[] args)
        {
            var leadId = Marketo.GetLeadIdByEmail("example@testemail.com"); //update email
            Console.WriteLine(leadId);
        }
    }

    public class Marketo
    {
        private static readonly HttpClient client = new();
        private static string RestUrl { get; set; } = "https://[TOKEN:MUNCHKIN].mktorest.com/rest/v1/"; //replace token
        private static string IdentityUrl { get; set; } = "https://[TOKEN:MUNCHKIN].mktorest.com/identity/oauth/token?client_id=[TOKEN:CLIENT_ID]&client_secret=[TOKEN:CLIENT_SECRET]&grant_type=client_credentials"; //replace tokens
        private static string? Token;

        private static async Task<string> Auth()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, IdentityUrl);
            request.Headers.Add("Authorization", "Bearer " + Token);

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException("Failed to get access token. Status code: " + response.StatusCode);
            }

            var responseData = await response.Content.ReadAsStringAsync();
            var marketoAuthResult = JsonConvert.DeserializeObject<MarketoAuthModel>(responseData);

            if (marketoAuthResult == null || string.IsNullOrEmpty(marketoAuthResult.access_token))
            {
                throw new UnauthorizedAccessException("Failed to obtain the access token from the response.");
            }

            return marketoAuthResult.access_token;
        }


        public static int GetLeadIdByEmail(string email)
        {
            var pathending = string.Concat("leads.json?filterType=email&filterValues=", email);
            var path = RestUrl + pathending.TrimStart('/');
            var response = SendGetRequest(path);

            return response.Result.result[0].id;

        }

        private static async Task<MarketoResponseModel> SendGetRequest(string endpoint)
        {
            try
            {
                int tryCount = 0;
                HttpResponseMessage? response = null;
                MarketoResponseModel? returnValue = null;
                var marketoErrorCode = string.Empty;

                const int MaxRetryCount = 3;
                const int RetryDelayMilliseconds = 1000;

                do
                {
                    if (Token == null || IsMarketoErrorRetryable(marketoErrorCode))
                    {
                        Token = await Auth();
                    }

                    var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                    request.Headers.Add("Authorization", "Bearer " + Token);

                    if (tryCount > 0)
                    {
                        Thread.Sleep(RetryDelayMilliseconds);
                    }

                    response = client.Send(request);
                    tryCount++;

                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = await response.Content.ReadAsStringAsync();
                        if (returnValue is null || returnValue.success)
                        {
                            returnValue = JsonConvert.DeserializeObject<MarketoResponseModel>(responseData);
                        }

                        if (returnValue?.errors?.Count > 0)
                        {
                            marketoErrorCode = returnValue.errors[0].code;
                        }
                    }
                    else
                    {
                        throw new HttpRequestException(response.ReasonPhrase);
                    }

                } while (IsMarketoErrorRetryable(marketoErrorCode) && tryCount <= MaxRetryCount);

                bool IsMarketoErrorRetryable(string errorCode)
                {
                    return errorCode == "602" || errorCode == "601";
                }
                return returnValue;
            }
            catch (Exception)
            {
                throw;
            }
        }

        //Models
        public class MarketoErrorResponseModel
        {
            public string? code { get; set; }
            public string? message { get; set; }
        }
        public class MarketoResponseModel
        {
            public string? requestId { get; set; }
            public bool success { get; set; }
            public dynamic? result { get; set; }
            public List<MarketoErrorResponseModel>? errors { get; set; }
        }

        class MarketoAuthModel
        {
            public string? access_token { get; set; }
            public string? token_type { get; set; }
            public int expires_in { get; set; }
            public string? scope { get; set; }
        }
    }
}