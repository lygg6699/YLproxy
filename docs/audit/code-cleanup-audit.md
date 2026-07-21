=== TODO 标记 ===

=== HACK/FIXME/XXX 标记 ===

=== 空catch块 ===
src/YLproxy.Proxy/ManagedProxyForwarder.cs:239:            catch (OperationCanceledException) { }
src/YLproxy.Proxy/ProxyProcessManager.cs:455:                catch (SocketException) { }
src/YLproxy.Api/ApiServer.cs:133:        try { await (_runTask ?? Task.CompletedTask); } catch (OperationCanceledException) { }
src/YLproxy.GUI/MainViewModel.cs:770:        catch { }

=== 测试项目检查 ===
11 tests/UnitTest1.cs
