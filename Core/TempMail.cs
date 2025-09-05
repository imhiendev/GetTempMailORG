using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GetTempMailORG
{
    public class TempMail : IDisposable
    {
        private readonly string _baseUrl = "https://web2.temp-mail.org";
        private readonly CustomHttpClient customHttpClient;
        private readonly CustomHttpClient customChromeHttpClient;
        private bool disposed = false;

        public TempMail(string proxyString = null)
        {
            var uri = new Uri(_baseUrl);
            
            customChromeHttpClient = new CustomHttpClient(uri.Host, uri.Port, JA3Fingerprint.CustomChrome, proxyString);
            customHttpClient = new CustomHttpClient(uri.Host, uri.Port, JA3Fingerprint.Default, proxyString);
        }

        public async Task<Mail?> GetMailAsync()
        {
            var attempts = new List<Func<Task<Mail?>>>
            {
                () => TryCustomChromeHttpClientApproach(),
                () => TryCustomHttpClientApproach()
            };

            foreach (var attempt in attempts)
            {
                try
                {
                    var result = await attempt();
                    if (result != null)
                    {
                        return result;
                    }
                }
                catch
                {
                    // Continue to next approach
                }
                
                await Task.Delay(2000);
            }

            return null;
        }

        private async Task<Mail?> TryCustomChromeHttpClientApproach()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/mailbox");
                
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                request.Headers.Add("Accept-Encoding", "gzip, deflate");
                request.Headers.Add("DNT", "1");
                request.Headers.Add("Sec-Fetch-Dest", "document");
                request.Headers.Add("Sec-Fetch-Mode", "navigate");
                request.Headers.Add("Sec-Fetch-Site", "none");
                request.Headers.Add("Sec-Fetch-User", "?1");
                request.Headers.Add("sec-ch-ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
                request.Headers.Add("sec-ch-ua-mobile", "?0");
                request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
                request.Headers.Add("Cache-Control", "no-cache");
                request.Headers.Add("Pragma", "no-cache");
                
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>());
                
                var response = await customChromeHttpClient.SendAsync(request, TimeSpan.FromSeconds(45));
                
                if (response.IsSuccessStatusCode)
                {
                    string html = await response.Content.ReadAsStringAsync();
                    return ExtractMailData(html);
                }
            }
            catch
            {
                // Ignore errors and try next approach
            }
            
            return null;
        }

        private async Task<Mail?> TryCustomHttpClientApproach()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/mailbox");
                
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                request.Headers.Add("Accept-Encoding", "gzip, deflate");
                request.Headers.Add("DNT", "1");
                request.Headers.Add("Sec-Fetch-Dest", "document");
                request.Headers.Add("Sec-Fetch-Mode", "navigate");
                request.Headers.Add("Sec-Fetch-Site", "none");
                request.Headers.Add("Sec-Fetch-User", "?1");
                request.Headers.Add("sec-ch-ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
                request.Headers.Add("sec-ch-ua-mobile", "?0");
                request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
                
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>());
                
                var response = await customHttpClient.SendAsync(request, TimeSpan.FromSeconds(45));
                
                if (response.IsSuccessStatusCode)
                {
                    string html = await response.Content.ReadAsStringAsync();
                    return ExtractMailData(html);
                }
            }
            catch
            {
                // Ignore errors
            }
            
            return null;
        }

        private Mail? ExtractMailData(string html)
        {
            try
            {
                var patterns = new[]
                {
                    new { TokenPattern = "\"token\":\"(.*?)\"", EmailPattern = "\"mailbox\":\"(.*?)\"" },
                    new { TokenPattern = @"token['"":]\s*['""]([^'""]+)['""]", EmailPattern = @"mailbox['"":]\s*['""]([^'""]+)['""]" },
                    new { TokenPattern = @"data-token=['""]([^'""]+)['""]", EmailPattern = @"data-email=['""]([^'""]+)['""]" },
                    new { TokenPattern = @"window\.token\s*=\s*['""]([^'""]+)['""]", EmailPattern = @"window\.email\s*=\s*['""]([^'""]+)['""]" },
                    new { TokenPattern = @"var\s+token\s*=\s*['""]([^'""]+)['""]", EmailPattern = @"var\s+email\s*=\s*['""]([^'""]+)['""]" }
                };

                foreach (var pattern in patterns)
                {
                    string token = Regex.Match(html, pattern.TokenPattern, RegexOptions.IgnoreCase).Groups[1].Value;
                    string email = Regex.Match(html, pattern.EmailPattern, RegexOptions.IgnoreCase).Groups[1].Value;
                    
                    if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(email))
                    {
                        return new Mail { Token = token, Email = email };
                    }
                }
            }
            catch
            {
                // Ignore extraction errors
            }
            
            return null;
        }

        public async Task<string?> GetOTPAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;
                
            // Try with Custom Chrome JA3 first
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/messages");
                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "*/*");
                request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                request.Headers.Add("Accept-Encoding", "gzip, deflate");
                request.Headers.Add("DNT", "1");
                request.Headers.Add("sec-ch-ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
                request.Headers.Add("sec-ch-ua-mobile", "?0");
                request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
                
                for (int i = 0; i < 15; i++)
                {
                    await Task.Delay(4000);
                    
                    var response = await customChromeHttpClient.SendAsync(request, TimeSpan.FromSeconds(30));
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        string? otp = ExtractOTP(content);
                        if (!string.IsNullOrEmpty(otp))
                        {
                            return otp;
                        }
                    }
                }
            }
            catch
            {
                // Continue to fallback
            }

            // Try with Default JA3 as fallback
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/messages");
                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "*/*");
                
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(5000);
                    
                    var response = await customHttpClient.SendAsync(request, TimeSpan.FromSeconds(30));
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        
                        string? otp = ExtractOTP(content);
                        if (!string.IsNullOrEmpty(otp))
                        {
                            return otp;
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        private string? ExtractOTP(string content)
        {
            string[] otpPatterns = {
                @"Your OTP is:?\s*(\d{4,8})",
                @"OTP[:\s]*(\d{4,8})",
                @"verification code[:\s]*(\d{4,8})",
                @"code[:\s]*(\d{4,8})",
                @"(\d{6})", // 6-digit OTP
                @"(\d{4})", // 4-digit OTP
                @">(\d{4,8})<", // OTP in tags
                @"<code>(\d{4,8})</code>"
            };
            
            foreach (var pattern in otpPatterns)
            {
                var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    string otp = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(otp) && otp.Length >= 4 && otp.Length <= 8)
                    {
                        return otp;
                    }
                }
            }
            
            return null;
        }

        // Synchronous wrapper methods
        public Mail? GetMail()
        {
            return GetMailAsync().GetAwaiter().GetResult();
        }

        public string? GetOTP(string token)
        {
            return GetOTPAsync(token).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    customHttpClient?.Dispose();
                    customChromeHttpClient?.Dispose();
                }
                disposed = true;
            }
        }

        public class Mail
        {
            public string? Token { get; set; }
            public string? Email { get; set; }
        }
    }
}