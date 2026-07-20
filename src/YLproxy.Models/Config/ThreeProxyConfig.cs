namespace YLproxy.Models.Config;

public class ThreeProxyConfig
{
    public string RuntimeDirectory { get; set; } = ConfigDefaults.ThreeProxyRuntime;
    public List<string> RequiredDlls { get; set; } = new List<string> { "FilePlugin.dll", "StringsPlugin.dll" };
}

