using System.Net;
using System.Text;
using Spectre.Console;

var proxyPort = args.Length > 0 ? int.Parse(args[0]) : 8888;
var targetUrl = args.Length > 1 ? args[1] : "http://localhost:5000";

AnsiConsole.Write(new FigletText("HTTP Proxy").Centered().Color(Color.Cyan1));
AnsiConsole.MarkupLine($"[green]Proxy listening on:[/] [yellow]http://localhost:{proxyPort}[/]");
AnsiConsole.MarkupLine($"[green]Forwarding to:[/] [yellow]{targetUrl}[/]");
AnsiConsole.MarkupLine("[grey]Press Ctrl+C to stop[/]\n");

var listener = new HttpListener();
listener.Prefixes.Add($"http://localhost:{proxyPort}/");
listener.Start();

var requestCount = 0;

while (true)
{
    var context = await listener.GetContextAsync();
    _ = Task.Run(async () => await HandleRequest(context, targetUrl, ++requestCount));
}

static async Task HandleRequest(HttpListenerContext context, string targetUrl, int requestId)
{
    var request = context.Request;
    var response = context.Response;
    
    var startTime = DateTime.Now;
    var requestColor = GetMethodColor(request.HttpMethod);
    
    // === DISPLAY REQUEST ===
    var requestPanel = new Panel(new Markup($"""
        [bold]{requestColor}]{request.HttpMethod}[/] [blue]{request.Url?.PathAndQuery}[/]
        [grey]From:[/] {request.RemoteEndPoint}
        [grey]Headers:[/] {request.Headers.Count}
        """))
    {
        Header = new PanelHeader($"[yellow]#{requestId}[/] REQUEST", Justify.Left),
        Border = BoxBorder.Rounded,
        BorderStyle = new Style(Color.Grey)
    };
    
    AnsiConsole.Write(requestPanel);
    
    // Display headers
    var headerTable = new Table()
        .BorderColor(Color.Grey)
        .AddColumn("Header")
        .AddColumn("Value");
    
    foreach (var key in request.Headers.AllKeys)
    {
        if (key != null)
            headerTable.AddRow($"[cyan]{key}[/]", $"[white]{request.Headers[key]}[/]");
    }
    
    AnsiConsole.Write(headerTable);
    
    // Display body if present
    string? requestBody = null;
    if (request.HasEntityBody)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        requestBody = await reader.ReadToEndAsync();
        
        if (!string.IsNullOrWhiteSpace(requestBody))
        {
            AnsiConsole.Write(new Panel(requestBody.Length > 500 
                ? $"[grey]{requestBody[..500]}...[/]\n[yellow](truncated, total: {requestBody.Length} chars)[/]" 
                : $"[white]{requestBody}[/]")
            {
                Header = new PanelHeader("Request Body"),
                Border = BoxBorder.Rounded
            });
        }
    }
    
    try
    {
        // === FORWARD REQUEST ===
        using var client = new HttpClient();
        var forwardedRequest = new HttpRequestMessage(
            new HttpMethod(request.HttpMethod), 
            targetUrl + request.Url?.PathAndQuery);
        
        // Copy headers (skip Host and Connection)
        foreach (var key in request.Headers.AllKeys)
        {
            if (key != null && 
                !key.Equals("Host", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("Connection", StringComparison.OrdinalIgnoreCase))
            {
                forwardedRequest.Headers.TryAddWithoutValidation(key, request.Headers[key]);
            }
        }
        
        // Copy body
        if (requestBody != null)
        {
            forwardedRequest.Content = new StringContent(
                requestBody, 
                Encoding.UTF8, 
                request.ContentType ?? "application/json");
        }
        
        // Send request
        var forwardedResponse = await client.SendAsync(forwardedRequest);
        var responseBody = await forwardedResponse.Content.ReadAsStringAsync();
        var elapsed = DateTime.Now - startTime;
        
        // === DISPLAY RESPONSE ===
        var statusColor = GetStatusColor((int)forwardedResponse.StatusCode);
        
        var responsePanel = new Panel(new Markup($"""
            [bold {statusColor}]{(int)forwardedResponse.StatusCode} {forwardedResponse.ReasonPhrase}[/]
            [grey]Duration:[/] [yellow]{elapsed.TotalMilliseconds:F0}ms[/]
            [grey]Content-Type:[/] {forwardedResponse.Content.Headers.ContentType}
            [grey]Content-Length:[/] {responseBody.Length} bytes
            """))
        {
            Header = new PanelHeader($"[yellow]#{requestId}[/] RESPONSE", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green)
        };
        
        AnsiConsole.Write(responsePanel);
        
        // Display response headers
        var responseHeaderTable = new Table()
            .BorderColor(Color.Grey)
            .AddColumn("Header")
            .AddColumn("Value");
        
        foreach (var header in forwardedResponse.Headers)
        {
            responseHeaderTable.AddRow($"[cyan]{header.Key}[/]", $"[white]{string.Join(", ", header.Value)}[/]");
        }
        
        if (responseHeaderTable.Rows.Count > 0)
            AnsiConsole.Write(responseHeaderTable);
        
        // Display response body
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            AnsiConsole.Write(new Panel(responseBody.Length > 1000 
                ? $"[grey]{responseBody[..1000]}...[/]\n[yellow](truncated, total: {responseBody.Length} chars)[/]" 
                : $"[white]{responseBody}[/]")
            {
                Header = new PanelHeader("Response Body"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Green)
            });
        }
        
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[grey]End of Request #{requestId}[/]").RuleStyle("grey dim"));
        AnsiConsole.WriteLine();
        
        // === RETURN RESPONSE TO CLIENT ===
        response.StatusCode = (int)forwardedResponse.StatusCode;
        response.ContentType = forwardedResponse.Content.Headers.ContentType?.ToString();
        
        // Copy response headers
        foreach (var header in forwardedResponse.Headers)
        {
            try
            {
                response.Headers.Add(header.Key, string.Join(", ", header.Value));
            }
            catch { /* Ignore protected headers */ }
        }
        
        var buffer = Encoding.UTF8.GetBytes(responseBody);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
    }
    catch (Exception ex)
    {
        AnsiConsole.Write(new Panel($"[red]{ex.Message}[/]")
        {
            Header = new PanelHeader($"[red]ERROR #{requestId}[/]"),
            Border = BoxBorder.Heavy,
            BorderStyle = new Style(Color.Red)
        });
        
        response.StatusCode = 502; // Bad Gateway
        var errorBytes = Encoding.UTF8.GetBytes($"Proxy Error: {ex.Message}");
        response.ContentLength64 = errorBytes.Length;
        await response.OutputStream.WriteAsync(errorBytes);
    }
    finally
    {
        response.Close();
    }
}

static string GetMethodColor(string method) => method switch
{
    "GET" => "green",
    "POST" => "blue",
    "PUT" => "yellow",
    "DELETE" => "red",
    "PATCH" => "orange1",
    _ => "white"
};

static string GetStatusColor(int status) => status switch
{
    >= 200 and < 300 => "green",
    >= 300 and < 400 => "yellow",
    >= 400 and < 500 => "orange1",
    >= 500 => "red",
    _ => "white"
};
