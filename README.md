# AtOCardRenderer
Render every card as individual components and more.

# Prerequisites
- BepInEx 6 is required to load this plugin. Follow the install instructions [here](https://docs.bepinex.dev/v6.0.0-pre.1/articles/user_guide/installation/unity_mono.html).
- .Net SDK is required to build the plugin. Download and install the latest recommended version from [here](https://dotnet.microsoft.com/en-us/download).

# Getting Started
1. Clone repository
    ```sh
    git clone https://github.com/ImDanTheDev/AtOCardRenderer.git
    cd AtOCardRenderer
    ```
2. Copy game libraries
    ```sh
    mkdir libs
    # This assumes the game is installed next to the AtOCardRenderer folder that the repository was cloned into in step 1. Adjust source copy path as needed.
    cp "..\Across the Obelisk\AcrossTheObelisk_Data\Managed\Assembly-CSharp.dll" libs\Assembly-CSharp.dll
    cp "..\Across the Obelisk\AcrossTheObelisk_Data\Managed\UnityEngine.UI.dll" libs\UnityEngine.UI.dll
    cp "..\Across the Obelisk\AcrossTheObelisk_Data\Managed\Unity.TextMeshPro.dll" libs\Unity.TextMeshPro.dll
    ```
3. Build
    ```shell
    dotnet deploy -c Release
    ```
4. Install plugin to Across the Obelisk
    ```sh
    cp bin\Release\netstandard2.1\publish\AtOCardRenderer.dll "..\Across the Obelisk\BepInEx\plugins\AtOCardRenderer.dll"
    cp bin\Release\netstandard2.1\publish\Magick.NET.Core.dll "..\Across the Obelisk\BepInEx\plugins\Magick.NET.Core.dll"
    cp bin\Release\netstandard2.1\publish\Magick.NET-Q16-AnyCPU.dll "..\Across the Obelisk\BepInEx\plugins\Magick.NET-Q16-AnyCPU.dll"
    ```
5. Launch game

# Info
- The rendered cards are saved to the RenderResults folder in the game install folder. `..\Across the Obelisk\RenderResults`
- A CSV summary of the rendered cards is saved to RenderSummary.csv in the game install folder. `..\Across the Obelisk\RenderSummary.csv`
- Press **Left Control** and **`** (Backtick) together to toggle plugin.