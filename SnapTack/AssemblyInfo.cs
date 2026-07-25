using System.Runtime.CompilerServices;
using System.Windows;

// テストから internal メンバー (Resources.Strings) を検証するため。
// Windows 非依存のロジック側は SnapTack.Core/AssemblyInfo.cs が同じ役割を担う
[assembly: InternalsVisibleTo("SnapTack.Tests")]

[assembly:ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
