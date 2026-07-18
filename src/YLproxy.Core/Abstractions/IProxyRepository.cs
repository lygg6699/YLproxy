using System;
using System.Collections.Generic;
using YLproxy.Models;

namespace YLproxy.Core.Abstractions;

public interface IProxyRepository : IDisposable
{
    List<ProxyItem> GetAll();
    ProxyItem? GetById(int id);

    int Add(ProxyItem proxy);
    void Update(ProxyItem proxy);
    void Delete(int id);

    int Count();
}

