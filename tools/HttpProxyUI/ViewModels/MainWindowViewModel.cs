using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HttpProxyUI.Models;
using Avalonia.Threading;

namespace HttpProxyUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<HttpRequest> _requests = new();
    [ObservableProperty] private HttpRequest? _selectedRequest;
    [ObservableProperty] private int _proxyPort = 8888;
    [ObservableProperty] private string _targetUrl = "http://localhost:5000";
    [ObservableProperty] private string _upstreamProxy = "";
    [ObservableProperty] private bool _useSystemProxy = true;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "Stopped";
    
    private ProxyService? _proxyService;
    
    public MainWindowViewModel()
    {
        LoadConfiguration();
    }
    
    [RelayCommand]
    private void Start()
    {
        if (IsRunning) return;
        
        SaveConfiguration();
        
        var upstreamProxyUrl = UseSystemProxy ? null : (string.IsNullOrWhiteSpace(UpstreamProxy) ? null : UpstreamProxy);
        
        _proxyService = new ProxyService(ProxyPort, TargetUrl, upstreamProxyUrl);
        _proxyService.RequestReceived += OnRequestReceived;
        _proxyService.ResponseReceived += OnResponseReceived;
        _proxyService.Start();
        
        IsRunning = true;
        
        var proxyInfo = UseSystemProxy 
            ? "System Proxy" 
            : (string.IsNullOrWhiteSpace(UpstreamProxy) ? "No upstream proxy" : UpstreamProxy);
        
        StatusText = $"Running on port {ProxyPort} → {TargetUrl} (via {proxyInfo})";
    }
    
    [RelayCommand]
    private void Stop()
    {
        if (!IsRunning || _proxyService == null) return;
        
        _proxyService.Stop();
        _proxyService = null;
        
        IsRunning = false;
        StatusText = "Stopped";
    }
    
    [RelayCommand]
    private void Clear()
    {
        Requests.Clear();
        SelectedRequest = null;
    }
    
    private void OnRequestReceived(HttpRequest request)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Requests.Insert(0, request);
            if (SelectedRequest == null)
                SelectedRequest = request;
        });
    }
    
    private void OnResponseReceived(HttpRequest request)
    {
        // Les propriétés sont déjà bindées, pas besoin de notifier
    }
    
    private void LoadConfiguration()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HttpProxyUI",
            "config.json");
        
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<ProxyConfig>(json);
                
                if (config != null)
                {
                    ProxyPort = config.ProxyPort;
                    TargetUrl = config.TargetUrl;
                    UpstreamProxy = config.UpstreamProxy ?? "";
                    UseSystemProxy = config.UseSystemProxy;
                }
            }
            catch { /* Ignore config errors */ }
        }
    }
    
    private void SaveConfiguration()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HttpProxyUI",
            "config.json");
        
        var configDir = Path.GetDirectoryName(configPath);
        if (configDir != null && !Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
        
        var config = new ProxyConfig
        {
            ProxyPort = ProxyPort,
            TargetUrl = TargetUrl,
            UpstreamProxy = string.IsNullOrWhiteSpace(UpstreamProxy) ? null : UpstreamProxy,
            UseSystemProxy = UseSystemProxy
        };
        
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(configPath, json);
        }
        catch { /* Ignore save errors */ }
    }
}

public class ProxyConfig
{
    public int ProxyPort { get; set; } = 8888;
    public string TargetUrl { get; set; } = "http://localhost:5000";
    public string? UpstreamProxy { get; set; }
    public bool UseSystemProxy { get; set; } = true;
}
