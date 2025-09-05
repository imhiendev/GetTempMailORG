using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GetTempMailORG
{
    public class CustomHttpClient : IDisposable
    {
        private readonly TlsClient _tlsClient;
        private bool disposed = false;

        public CustomHttpClient(string host, int port = 443, JA3Fingerprint ja3Fingerprint = null, string proxyString = null)
        {
            _tlsClient = new TlsClient(host, port, ja3Fingerprint, proxyString);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                _tlsClient?.Dispose();
                disposed = true;
            }
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                var requestString = ConvertHttpRequestMessageToString(request);
                var responseString = await _tlsClient.SendRequestAsync(requestString, timeout);

                if (string.IsNullOrEmpty(responseString))
                {
                    throw new Exception("Empty response received from server");
                }

                return ConvertStringToHttpResponseMessage(responseString);
            }
            catch (Exception ex)
            {
                throw new Exception($"HTTP request failed: {ex.Message}", ex);
            }
        }

        private string ConvertHttpRequestMessageToString(HttpRequestMessage request)
        {
            using (var sw = new StringWriter())
            {
                var path = request.RequestUri.PathAndQuery;
                if (string.IsNullOrEmpty(path)) path = "/";
                
                sw.WriteLine($"{request.Method} {path} HTTP/1.1");
                sw.WriteLine($"Host: {request.RequestUri.Host}");
                sw.WriteLine("Connection: close");

                foreach (var header in request.Headers) 
                {
                    if (header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
                        header.Key.Equals("Upgrade", StringComparison.OrdinalIgnoreCase) ||
                        header.Key.Equals("HTTP2-Settings", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    
                    sw.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }

                if (request.Content != null)
                {
                    foreach (var header in request.Content.Headers)
                    {
                        sw.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
                    }
                }

                sw.WriteLine();

                if (request.Content != null) 
                {
                    var contentTask = request.Content.ReadAsStringAsync();
                    contentTask.Wait();
                    var content = contentTask.Result;
                    
                    if (!string.IsNullOrEmpty(content))
                    {
                        sw.Write(content);
                    }
                }

                return sw.ToString();
            }
        }

        private HttpResponseMessage ConvertStringToHttpResponseMessage(string response)
        {
            var httpResponse = new HttpResponseMessage();

            try
            {
                using (var sr = new StringReader(response))
                {
                    var statusLine = sr.ReadLine();
                    if (string.IsNullOrEmpty(statusLine))
                    {
                        throw new Exception("No status line found in response");
                    }

                    if (!statusLine.StartsWith("HTTP/1."))
                    {
                        throw new Exception($"Expected HTTP/1.x response, got: {statusLine}");
                    }

                    var statusLineParts = statusLine.Split(' ');
                    if (statusLineParts.Length >= 3)
                    {
                        if (int.TryParse(statusLineParts[1], out int statusCode))
                        {
                            httpResponse.StatusCode = (HttpStatusCode)statusCode;
                        }
                        else
                        {
                            httpResponse.StatusCode = HttpStatusCode.OK;
                        }
                    }

                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            break;

                        var headerParts = line.Split(new[] { ':' }, 2);
                        if (headerParts.Length == 2)
                        {
                            var headerName = headerParts[0].Trim();
                            var headerValue = headerParts[1].Trim();
                            
                            try
                            {
                                httpResponse.Headers.TryAddWithoutValidation(headerName, headerValue);
                            }
                            catch
                            {
                                // Ignore invalid headers
                            }
                        }
                    }

                    var body = sr.ReadToEnd();
                    httpResponse.Content = new StringContent(body ?? "", Encoding.UTF8);
                }

                return httpResponse;
            }
            catch (Exception)
            {
                var fallbackResponse = new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(response, Encoding.UTF8)
                };
                
                return fallbackResponse;
            }
        }
    }
}
