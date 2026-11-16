using System.Text.Json;

namespace ACAI2.Bridge;

internal sealed class BridgeConfig
{
    public string ListenPrefix { get; set; } = "http://127.0.0.1:5052/";
    public int ClientStaleSeconds { get; set; } = 30;
    public int RegistryPrintSeconds { get; set; } = 0;

    public static BridgeConfig Load()
    {
        string path = Environment.GetEnvironmentVariable("ACAI2_CONFIG_PATH") ?? @"C:\ACAI2\Config\acai2.json";

        try
        {
            if (!File.Exists(path))
                return new BridgeConfig();

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var config = new BridgeConfig();

            if (root.TryGetProperty("bridge", out var bridge))
            {
                if (bridge.TryGetProperty("listenPrefix", out var prefix) &&
                    !string.IsNullOrWhiteSpace(prefix.GetString()))
                {
                    config.ListenPrefix = prefix.GetString()!;
                }

                if (bridge.TryGetProperty("clientStaleSeconds", out var stale) &&
                    stale.TryGetInt32(out var staleSeconds) && staleSeconds >= 10)
                {
                    config.ClientStaleSeconds = staleSeconds;
                }

                if (bridge.TryGetProperty("registryPrintSeconds", out var print) &&
                    print.TryGetInt32(out var printSeconds) && printSeconds >= 0)
                {
                    config.RegistryPrintSeconds = printSeconds;
                }
            }

            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[CONFIG] Could not read C:\\ACAI2\\Config\\acai2.json: " + ex.Message);
            Console.WriteLine("[CONFIG] Using Phase 1 defaults.");
            return new BridgeConfig();
        }
    }
}
