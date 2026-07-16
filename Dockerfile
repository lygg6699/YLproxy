# ---- Build & Test Stage ----
# Note: WPF GUI cannot run on Linux; this stage validates library/test projects only.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore YLproxy.sln
RUN dotnet build YLproxy.sln --configuration Release -warnaserror
RUN dotnet test tests/YLproxy.Tests.csproj --configuration Release --no-build --filter "Category=Unit" || true
RUN dotnet test tests/YLproxy.Tests.csproj --configuration Release --no-build

# ---- Publish Stage (Windows-only, for reference) ----
# FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
# RUN dotnet publish src/YLproxy.GUI -c Release -r win-x64 --self-contained true -o /out
