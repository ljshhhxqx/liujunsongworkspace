using Config;
using UnityEngine;

public interface IConfigProvider
{
    T GetConfig<T>() where T : ConfigBase, new();
}

public class ConfigProvider : IConfigProvider
{
    private readonly ConfigManager _configManager;

    public ConfigProvider(ConfigManager configManager)
    {
        _configManager = configManager;
    }

    public T GetConfig<T>() where T : ConfigBase, new()
    {
        return _configManager.GetConfig<T>();
    }
}