# Third-Party Notices

**更新时间：** 2026-07-19

## 3proxy 0.9.7

YLproxy uses 3proxy as its local proxy engine. The Windows x64 runtime is not
committed to this source repository. Run `scripts/prepare-runtime.ps1` to
fetch the pinned official release into `runtime/3proxy/bin64/`.

- Project: https://github.com/3proxy/3proxy
- Release: https://github.com/3proxy/3proxy/releases/tag/0.9.7
- License text: `runtime/3proxy/copying`
- Archive: `3proxy-0.9.7-x64.zip`
- SHA-256: `e94f4967f46f859d49345afdcb1830cf9b042b5b9fdfc3bef33d65e95715cae3`

The 3proxy license and copyright notices are retained in `runtime/3proxy/`
and must remain available when distributing a build that includes the runtime.

---

## .NET Dependencies

YLproxy uses various .NET NuGet packages as specified in `Directory.Packages.props`:
- Microsoft.Extensions.DependencyInjection 9.0.0
- Microsoft.Data.Sqlite 2.1.12 (for potential SQLite migration)
- xUnit 2.4.2 (for testing)
- Swashbuckle.AspNetCore 6.5.0 (for API documentation)

These packages are licensed under their respective licenses, typically MIT or Apache-2.0.
For detailed license information, please refer to the NuGet package pages.

---

## Additional Third-Party Components

### Windows DPAPI
YLproxy uses Windows Data Protection API (DPAPI) for credential encryption.
This is a native Windows component and does not require additional licensing.

### Scriban (Planned)
YLproxy may use Scriban for 3proxy configuration templating (Phase C3.4).
- Project: https://github.com/scriban/scriban
- License: BSD-2-Clause

### BenchmarkDotNet (Planned)
YLproxy may use BenchmarkDotNet for performance testing (Phase C3.6).
- Project: https://github.com/dotnet/BenchmarkDotNet
- License: BSD-3-Clause

---

**Note:** This file should be updated when new third-party dependencies are added to the project.
