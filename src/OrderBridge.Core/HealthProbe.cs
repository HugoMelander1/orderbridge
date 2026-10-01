namespace OrderBridge.Core;

public static class HealthProbe
{
    public static async Task<int> Run()
    {
        try { using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) }; return (await http.GetAsync("http://127.0.0.1:8080/health")).IsSuccessStatusCode ? 0 : 1; }
        catch { return 1; }
    }
}
