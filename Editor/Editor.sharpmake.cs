using Sharpmake;

namespace GameBench
{
    [Generate]
    public class Editor : CSharpProject
    {
        public Editor() : base(typeof(GameBenchTarget))
        {
            Name = "Editor";
            RootPath = @"[project.SharpmakeCsPath]";
            SourceRootPath = @"[project.SharpmakeCsPath]\Source";

            IsFileNameToLower = false;
            IsTargetFileNameToLower = false;
            ProjectSchema = CSharpProjectSchema.NetCore;
            CustomProperties["Platforms"] = "x64";
            CustomProperties["Configurations"] = "Debug Editor;Release Editor";
            CustomProperties["PlatformTarget"] = "x64";
            AddTargets(new GameBenchTarget(BuildMode.Editor).ManagedTarget);
        }

        [Configure]
        public void ConfigureAll(Configuration conf, GameBenchTarget target)
        {
            conf.ProjectFileName = "[project.Name]";
            conf.ProjectPath = @"[project.SharpmakeCsPath]\Intermediate\ProjectFiles";

            conf.IntermediatePath = @"[project.SharpmakeCsPath]\Intermediate\Build\[project.Name]\[target.Platform]_[target.OutputFolder]";
            conf.TargetPath = @"[project.SharpmakeCsPath]\Binaries\[target.Platform]\[target.OutputFolder]";
            conf.Output = Configuration.OutputType.DotNetWindowsApp;

            //@TODO: Currently, disabling engine project as editor right now purely visual. Need to solve:
            //@TODO: Figure out a model, similiar to unreal engine that even with a game project we still have one application. 
            //       With editor, same BUT we will interop the function. Right now, We created 4 functions for interop.
            //       Ideally, we just have ONE interop function and stuff like tick, shutdown etx just handle by editor telling the engine to shut down or tick like giving messages
            //@TODO: Figure out a design. Using UnrealEngine as inspiration, the Engine class acts as the base Application class, and depends on Runtime or Editor, we will have 
            //       EditorEngine or GameEngine
            //conf.AddPrivateDependency<Engine>(target.Clone(DotNetFramework.v4_7_2), DependencySetting.OnlyBuildOrder);

            conf.CsprojUserFile = new Configuration.CsprojUserFileSettings
            {
                EnableUnmanagedDebug = true,
                StartArguments = ""
            };
            string BinariesDir = @"$(SolutionDir)Engine\Binaries\win64\$(Configuration)";
            conf.EventPostBuild.Add($@"copy /Y ""{BinariesDir}\Engine.dll"" ""$(TargetDir)Engine.dll""");
            conf.EventPostBuild.Add($@"if exist ""{BinariesDir}\Engine.pdb"" copy /Y ""{BinariesDir}\Engine.pdb"" ""$(TargetDir)Engine.pdb""");
            conf.EventPostBuild.Add($@"xcopy ""{BinariesDir}\Shaders"" ""$(TargetDir)Shaders"" /E /I /Y /D");
            conf.EventPostBuild.Add(@"xcopy ""$(SolutionDir)packages\Microsoft.Direct3D.D3D12.1.619.5\build\native\bin\x64\*.dll"" ""$(TargetDir)D3D12"" /I /Y /D");
            conf.EventPostBuild.Add(@"xcopy ""$(SolutionDir)Editor\Resources"" ""$(TargetDir)Resources"" /E /I /Y /D");

            conf.ReferencesByNuGetPackage.Add("Avalonia", "12.0.5");
            conf.ReferencesByNuGetPackage.Add("Avalonia.Desktop", "12.0.5");
            conf.ReferencesByNuGetPackage.Add("Avalonia.Themes.Fluent", "12.0.5");
            conf.ReferencesByNuGetPackage.Add("Avalonia.Fonts.Inter", "12.0.5");

            conf.ReferencesByNuGetPackage.Add("Dock.Avalonia", "12.0.0.2");
            conf.ReferencesByNuGetPackage.Add("Dock.Model.Mvvm", "12.0.0.2");
            conf.ReferencesByNuGetPackage.Add("Dock.Avalonia.Themes.Fluent", "12.0.0.2");
        }
    }
}
