using System;
using System.Collections.Generic;

namespace YLproxy.Infrastructure
{
    public interface IConfigService
    {
        T GetSection<T>(string sectionName) where T : class, new();
        void UpdateSection<T>(string sectionName, T settings) where T : class;
        void Reload();
    }
}