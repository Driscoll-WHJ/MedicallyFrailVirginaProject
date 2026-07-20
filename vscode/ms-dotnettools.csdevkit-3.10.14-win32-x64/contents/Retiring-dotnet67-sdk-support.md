# 📢 DEPRECATION ANNOUNCEMENT: Developing with .NET 6 and .NET 7 SDKs No Longer Supported

Support for developing with .NET 6 and .NET 7 SDKs will be retired in the **January 2026 release** of C# Dev Kit and C# extension. These SDKs reached the end of their official support lifecycle in May 2024 and November 2024, respectively. 

## ✅ What This Means for You
- Use of out-of-support .NET versions may put your applications, application data, and computing environment at risk. **You are strongly recommended to not use out-of-support software.**

- If you are **building applications using a .NET 7 or lower SDK**, you may experience a **degraded code-editing experience** in VS Code. 
  - IntelliSense, debugging, and certain project system features may not work as expected.
  - Bugs or issues that are exclusive to .NET 7 or lower will not be fixed. 

- **Targeting .NET 6 or .NET 7 remains fully supported.**
  - You can still build and run apps that target these frameworks as long as you use a supported SDK (e.g., .NET 8 or later).

## 🔍 Why This Change?
.NET 6 and .NET 7 both reached end of support over a year ago. Continuing to support these SDKs requires maintaining outdated components, which impacts performance and reliability. By focusing on supported SDKs, we ensure the best experience for modern C# development.

## ✅ Recommended Action
- Review the [.NET Support Policy](https://dotnet.microsoft.com/platform/support/policy).
- Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
- If you also want help upgrading your target framework, install the [GitHub Copilot app modernization for .NET extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.vscode-dotnet-modernize).
  - The extension requires a GitHub Copilot subscription. If you do not have a subscription, you can obtain one here: [Get access to Copilot](https://docs.github.com/copilot/get-started/what-is-github-copilot#getting-access-to-copilot)

### Questions?
Please open an issue in the [C# Dev Kit GitHub repository](https://github.com/microsoft/vscode-dotnettools/issues).
