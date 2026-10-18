# dxpackages

Local NuGet folder for the DevExpress packages (DevExpress Reporting) used when building the Docker image.
They are licensed DevExpress files, so only this README is committed. See `.gitignore`.

Fill it after the project restores on your machine (from the DevExpress installer or the DevExpress NuGet feed):

```powershell
dotnet restore
./tools/collect-devexpress-packages.ps1
```

Instead, you can give the Docker build your DevExpress NuGet feed URL as a secret. See the README in the repo root.
