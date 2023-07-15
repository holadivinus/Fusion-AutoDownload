using System.Reflection;
using System.Runtime.InteropServices;
using FusionAutoDownload;
using MelonLoader;
using System;



[assembly: MelonColor(ConsoleColor.White)]
[assembly: MelonInfo(typeof(AutoDownloadMelon), "Fusion Autodownloader", ModVersion.VERSION, "Holadivinus#holadivinus")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

[assembly: MelonPriority(-9000)]

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Fusion-AutoDownload")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("Fusion-AutoDownload")]
[assembly: AssemblyCopyright("Copyright ©  2023")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("fc79b732-5ebe-4a9b-a19d-f0cd0716f661")]


[assembly: AssemblyVersion("1." + ModVersion.VERSION)]
[assembly: AssemblyFileVersion("1." + ModVersion.VERSION)]

public static class ModVersion
{
    public const string VERSION = "0.1.0";
}