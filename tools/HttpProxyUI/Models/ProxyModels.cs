using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HttpProxyUI.Models;

public partial class HttpRequest : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _method = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private DateTime _timestamp;
    [ObservableProperty] private string _remoteEndPoint = string.Empty;
    [ObservableProperty] private ObservableCollection<KeyValuePair<string, string>> _headers = new();
    [ObservableProperty] private string? _body;
    [ObservableProperty] private int _statusCode;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private double _duration;
    [ObservableProperty] private ObservableCollection<KeyValuePair<string, string>> _responseHeaders = new();
    [ObservableProperty] private string? _responseBody;
    [ObservableProperty] private long _responseSize;
    
    public string DisplayText => $"{Method} {Path}";
    public string StatusDisplay => StatusCode > 0 ? $"{StatusCode} {StatusText}" : "Pending...";
    public string TimeDisplay => Timestamp.ToString("HH:mm:ss.fff");
    public string DurationDisplay => Duration > 0 ? $"{Duration:F0}ms" : "-";
}

public class ProxyService
{
    private readonly HttpListener _listener;
    private readonly string _targetUrl;
    private readonly IWebProxy? _upstreamProxy;
    private int _requestCounter;
    
    public event Action<HttpRequest>? RequestReceived;
    public event Action<HttpRequest>? ResponseReceived;
    
    public ProxyService(int port, string targetUrl, string? upstreamProxyUrl = null)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _targetUrl = targetUrl;
        
        // Configuration du proxy en amont (upstream proxy)
        if (!string.IsNullOrWhiteSpace(upstreamProxyUrl))
        {
            _upstreamProxy = new WebProxy(upstreamProxyUrl);
        }
        else
        {
            // Utiliser le proxy système par défaut
            _upstreamProxy = WebRequest.GetSystemWebProxy();
        }
    }
    
    public void Start()
    {
        _listener.Start();
        Task.Run(ListenLoop);
    }
    
    public void Stop()
    {
        _listener.Stop();
    }
    
    private async Task ListenLoop()
    {
        while (_listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context));
            }
            catch { /* Listener stopped */ }
        }
    }
    
    private async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        
        // Déterminer l'URL cible : soit depuis l'URL absolue (proxy mode), soit construite
        string targetUrl;
        if (request.Url?.IsAbsoluteUri == true && !string.IsNullOrEmpty(request.Url.Host))
        {
            // Mode proxy : Firefox envoie l'URL complète (http://example.com/path)
            targetUrl = request.Url.ToString();
        }
        else if (request.RawUrl != null && request.RawUrl.StartsWith("http://"))
        {
            // Mode proxy alternatif : RawUrl contient l'URL complète
            targetUrl = request.RawUrl;
        }
        else
        {
            // Mode forward : URL relative, on ajoute le target configuré
            targetUrl = _targetUrl + request.Url?.PathAndQuery;
        }
        
        var httpRequest = new HttpRequest
        {
            Id = ++_requestCounter,
            Method = request.HttpMethod,
            Url = targetUrl,
            Path = request.Url?.PathAndQuery ?? request.RawUrl ?? "/",
            Timestamp = DateTime.Now,
            RemoteEndPoint = request.RemoteEndPoint?.ToString() ?? ""
        };
        
        // Capture request headers
        foreach (var key in request.Headers.AllKeys)
        {
            if (key != null)
                httpRequest.Headers.Add(new KeyValuePair<string, string>(key, request.Headers[key] ?? ""));
        }
        
        // Capture request body
        string? requestBody = null;
        if (request.HasEntityBody)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            requestBody = await reader.ReadToEndAsync();
            httpRequest.Body = requestBody;
        }
        
        RequestReceived?.Invoke(httpRequest);
        
        try
        {
            var startTime = DateTime.Now;
            
            // Créer HttpClientHandler avec le proxy upstream
            var handler = new HttpClientHandler
            {
                Proxy = _upstreamProxy,
                UseProxy = _upstreamProxy != null,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // Accept all SSL
            };
            
            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(30);
            
            var forwardedRequest = new HttpRequestMessage(
                new HttpMethod(request.HttpMethod),
                targetUrl);
            
            // Copier tous les headers sauf ceux gérés automatiquement
            var skipHeaders = new[] 
            { 
                "Host", "Connection", "Proxy-Connection", "Content-Length", 
                "Transfer-Encoding", "Expect", "Keep-Alive", "TE", "Trailer", "Upgrade"
            };
            
            foreach (var key in request.Headers.AllKeys)
            {
                if (key != null && !skipHeaders.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        // Essayer d'abord comme header de requête
                        if (!forwardedRequest.Headers.TryAddWithoutValidation(key, request.Headers[key]))
                        {
                            // Si ça échoue, ce sera ajouté au content header plus tard
                        }
                    }
                    catch { /* Skip invalid headers */ }
                }
            }
            
            // Copier le body si présent
            if (!string.IsNullOrEmpty(requestBody) && 
                request.HttpMethod != "GET" && 
                request.HttpMethod != "HEAD" &&
                request.HttpMethod != "TRACE")
            {
                var contentType = request.ContentType ?? "application/octet-stream";
                forwardedRequest.Content = new StringContent(requestBody, Encoding.UTF8);
                forwardedRequest.Content.Headers.ContentType = 
                    System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
            }
            
            var forwardedResponse = await client.SendAsync(forwardedRequest, HttpCompletionOption.ResponseContentRead);
            var responseBody = await forwardedResponse.Content.ReadAsStringAsync();
            var elapsed = DateTime.Now - startTime;
            
            httpRequest.StatusCode = (int)forwardedResponse.StatusCode;
            httpRequest.StatusText = forwardedResponse.ReasonPhrase ?? "";
            httpRequest.Duration = elapsed.TotalMilliseconds;
            httpRequest.ResponseBody = responseBody;
            httpRequest.ResponseSize = responseBody.Length;
            
            // Copier response headers
            foreach (var header in forwardedResponse.Headers)
            {
                httpRequest.ResponseHeaders.Add(
                    new KeyValuePair<string, string>(header.Key, string.Join(", ", header.Value)));
            }
            foreach (var header in forwardedResponse.Content.Headers)
            {
                httpRequest.ResponseHeaders.Add(
                    new KeyValuePair<string, string>(header.Key, string.Join(", ", header.Value)));
            }
            
            ResponseReceived?.Invoke(httpRequest);
            
            // Retourner la réponse au client
            response.StatusCode = (int)forwardedResponse.StatusCode;
            
            var skipResponseHeaders = new[] { "Transfer-Encoding", "Content-Length" };
            
            foreach (var header in forwardedResponse.Headers)
            {
                if (!skipResponseHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        response.Headers.Add(header.Key, string.Join(", ", header.Value));
                    }
                    catch { /* Ignore protected headers */ }
                }
            }
            
            foreach (var header in forwardedResponse.Content.Headers)
            {
                if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    response.ContentType = string.Join(", ", header.Value);
                }
                else if (!skipResponseHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        response.Headers.Add(header.Key, string.Join(", ", header.Value));
                    }
                    catch { /* Ignore protected headers */ }
                }
            }
            
            // Envoyer le body de la réponse
            if (!string.IsNullOrEmpty(responseBody))
            {
                var buffer = Encoding.UTF8.GetBytes(responseBody);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer);
            }
            else
            {
                response.ContentLength64 = 0;
            }
        }
        catch (Exception ex)
        {
            httpRequest.StatusCode = 502;
            httpRequest.StatusText = "Bad Gateway";
            httpRequest.ResponseBody = $"Proxy Error: {ex.Message}\n\nTarget URL: {targetUrl}\n\nStack: {ex.StackTrace}";
            httpRequest.Duration = 0;
            
            ResponseReceived?.Invoke(httpRequest);
            
            response.StatusCode = 502;
            response.ContentType = "text/plain; charset=utf-8";
            var errorMessage = $"Proxy Error: {ex.Message}\nTarget: {targetUrl}";
            var errorBytes = Encoding.UTF8.GetBytes(errorMessage);
            response.ContentLength64 = errorBytes.Length;
            
            try
            {
                await response.OutputStream.WriteAsync(errorBytes);
            }
            catch { /* Client disconnected */ }
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch { /* Already closed */ }
        }
    }
}
