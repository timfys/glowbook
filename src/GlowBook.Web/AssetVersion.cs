namespace GlowBook.Web;

public sealed class AssetVersion
{
    public string Value { get; }

    public AssetVersion(IHostEnvironment env, IConfiguration config)
    {
        var raw =
            config["ASSET_VERSION"]
            ?? Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA")
            ?? Environment.GetEnvironmentVariable("RAILWAY_DEPLOYMENT_ID")
            ?? Environment.GetEnvironmentVariable("SOURCE_VERSION")
            ?? typeof(AssetVersion).Assembly.GetName().Version?.ToString()
            ?? "dev";

        // Short stable token for query strings
        Value = new string(raw.Where(char.IsLetterOrDigit).Take(16).ToArray());
        if (string.IsNullOrEmpty(Value))
            Value = env.IsDevelopment() ? DateTime.UtcNow.Ticks.ToString("x") : "1";
    }
}
