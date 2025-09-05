using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Linq;

namespace GetTempMailORG
{
    public class JA3Fingerprint
    {
        public string TlsVersion { get; set; }
        public List<int> CipherSuites { get; set; }
        public List<int> Extensions { get; set; }
        public List<int> EllipticCurves { get; set; }
        public List<int> EllipticCurvePointFormats { get; set; }
        public List<string> ApplicationProtocols { get; set; }

        public JA3Fingerprint()
        {
            CipherSuites = new List<int>();
            Extensions = new List<int>();
            EllipticCurves = new List<int>();
            EllipticCurvePointFormats = new List<int>();
            ApplicationProtocols = new List<string>();
        }

        public static JA3Fingerprint ParseFromString(string ja3String)
        {
            var parts = ja3String.Split(',');
            if (parts.Length != 5)
                throw new ArgumentException("Invalid JA3 string format. Expected 5 comma-separated parts.");

            var fingerprint = new JA3Fingerprint
            {
                TlsVersion = parts[0],
                CipherSuites = ParseIntList(parts[1]),
                Extensions = ParseIntList(parts[2]),
                EllipticCurves = ParseIntList(parts[3]),
                EllipticCurvePointFormats = ParseIntList(parts[4]),
                ApplicationProtocols = new List<string> { "http/1.1" }
            };

            return fingerprint;
        }

        private static List<int> ParseIntList(string input)
        {
            if (string.IsNullOrEmpty(input))
                return new List<int>();

            return input.Split('-')
                       .Where(s => !string.IsNullOrEmpty(s))
                       .Select(int.Parse)
                       .ToList();
        }

        public static JA3Fingerprint Default => new JA3Fingerprint
        {
            TlsVersion = "771",
            CipherSuites = new List<int>
            {
                0x1301, 0x1302, 0x1303, 0xc02b, 0xc02f, 0xc02c, 0xc030, 0xcca9, 0xcca8
            },
            Extensions = new List<int>
            {
                0, 23, 65281, 10, 11, 35, 22, 23, 13, 43, 51
            },
            EllipticCurves = new List<int> { 29, 23, 24 },
            EllipticCurvePointFormats = new List<int> { 0 },
            ApplicationProtocols = new List<string> { "http/1.1" }
        };

        public static JA3Fingerprint CustomChrome => ParseFromString("769,4865-4866-4867-49195-49199-49196-49200-52393-52392-49171-49172-156-157-47-53,0-23-65281-10-11-35-16-5-13-18-51-45-43-27-21-41-28-19,29-23-24,0");

        public List<int> GetCipherSuites()
        {
            return new List<int>(CipherSuites);
        }

        public SslProtocols GetSslProtocols()
        {
            return TlsVersion switch
            {
                "769" => SslProtocols.Tls12 | SslProtocols.Tls13,
                "770" => SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13,
                "771" => SslProtocols.Tls12 | SslProtocols.Tls13,
                "772" => SslProtocols.Tls13,
                _ => SslProtocols.Tls12 | SslProtocols.Tls13
            };
        }

        public List<string> GetApplicationProtocols()
        {
            return ApplicationProtocols;
        }

        public string GenerateJA3String()
        {
            var cipherSuites = string.Join("-", CipherSuites);
            var extensions = string.Join("-", Extensions);
            var ellipticCurves = string.Join("-", EllipticCurves);
            var pointFormats = string.Join("-", EllipticCurvePointFormats);

            return $"{TlsVersion},{cipherSuites},{extensions},{ellipticCurves},{pointFormats}";
        }

        public string GetDisplayInfo()
        {
            return $"TLS: {TlsVersion}, Ciphers: {CipherSuites.Count}, Extensions: {Extensions.Count}, Curves: {EllipticCurves.Count}, Protocol: HTTP/1.1";
        }
    }
}