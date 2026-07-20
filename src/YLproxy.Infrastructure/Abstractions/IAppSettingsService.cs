using YLproxy.Models.Config;

namespace YLproxy.Infrastructure.Abstractions;

public interface IAppSettingsService
{
    T GetSection<T>(string sectionName) where T : class, new();
    AppSettingsConfig GetConfig();
    void Reload();
}

