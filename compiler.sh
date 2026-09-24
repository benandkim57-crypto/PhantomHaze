name: Build PhantomHaze

on:
  push:
    branches: [ main ]
  workflow_dispatch: {}

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      # Self-contained + single-file: the result is one .exe that runs on
      # any Windows 11 x64 machine with no separate .NET install required.
      - name: Publish
        run: >
          dotnet publish PhantomHaze.csproj
          -c Release
          -r win-x64
          --self-contained true
          -p:PublishSingleFile=true
          -p:IncludeNativeLibrariesForSelfExtract=true
          -o publish

      - uses: actions/upload-artifact@v4
        with:
          name: PhantomHaze-win-x64
          path: publish/PhantomHaze.exe