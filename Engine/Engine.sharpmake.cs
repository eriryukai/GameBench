using Sharpmake;

namespace MortarEngine
{
    [Generate]
    public class Engine : Project
    {
        public Engine() : base(typeof(MortarEngineTarget))
        {
            Name = "Engine";
            SourceRootPath = @"[project.SharpmakeCsPath]\Source";

            IsFileNameToLower = false;
            IsTargetFileNameToLower = false;

            AddTargets(new MortarEngineTarget());
        }

        [Configure]
        public void ConfigureAll(Configuration conf, MortarEngineTarget target)
        {
            conf.SolutionFolder = "Engine";
            conf.ProjectFileName = "[project.Name]_[target.DevEnv]_[target.Platform]";
            conf.ProjectPath = @"[project.SharpmakeCsPath]\Intermediate\ProjectFiles";

            conf.IntermediatePath = @"[project.SharpmakeCsPath]\Intermediate\Build\[project.Name]\[target.Platform]_[conf.Name]";
            conf.TargetPath = @"[project.SharpmakeCsPath]\Binaries\[target.Platform]\[conf.Name]";

            conf.PrecompHeader = "stdafx.h";
            conf.PrecompSource = "stdafx.cpp";

            // Public framework headers are exported to game modules such as EOC.
            conf.IncludePaths.Add(@"[project.SharpmakeCsPath]\Source\Public");

            // Runtime internals remain private to the Engine library.
            conf.IncludePrivatePaths.Add(@"[project.SharpmakeCsPath]\Source\Private");

            conf.Options.Add(Options.Vc.General.PlatformToolset.v145);
            conf.Options.Add(Options.Vc.Compiler.CppLanguageStandard.Latest);

            InitializePlatform(conf, target);
            InitializeDependencies(conf, target.NativeToolTarget);
            ConfigureRuntimeLibrary(conf, target);
        }

        void InitializePlatform(Configuration conf, Target target)
        {
            if (target.Platform == Platform.win64)
            {
                conf.Defines.Add("MORTARENGINE_PLATFORM_WINDOWS");
                conf.Options.Add(Options.Vc.Linker.SubSystem.Windows);

                // FileSystem / nativefiledialog-extended platform APIs.
                conf.LibraryFiles.Add("Advapi32.lib"); // Registry environment variables
                conf.LibraryFiles.Add("Ole32.lib");    // COM (IFileDialog)
                conf.LibraryFiles.Add("Shell32.lib");  // ShellExecute / SHGetKnownFolderPath
                conf.LibraryFiles.Add("User32.lib");   // SendMessageTimeoutA
            }

            ConfigureGLFW(conf, target);
        }

        void ConfigureGLFW(Configuration conf, Target target)
        {
            conf.Defines.Add("_GLFW_WIN32");

            conf.IncludePaths.Add(@"[project.SharpmakeCsPath]\ThirdParty\GLFW\Window\include");
            conf.LibraryPaths.Add(@"[project.SharpmakeCsPath]\ThirdParty\GLFW\Window\lib-vc2026");
            conf.LibraryFiles.Add("glfw3_mt.lib");
        }

        void InitializeDependencies(Configuration conf, Target target)
        {
            conf.ReferencesByNuGetPackage.Add("Microsoft.Direct3D.D3D12", "1.619.5");

            // Engine is a static runtime library. Its link dependencies are public
            // so the final game executable inherits the complete link line.
            conf.AddPublicDependency<DirectX12>(target);
            conf.AddPublicDependency<ImGui>(target);
            conf.AddPublicDependency<Choc>(target);
            conf.AddPublicDependency<NFDExtended>(target);
        }

        void ConfigureRuntimeLibrary(Configuration conf, MortarEngineTarget target)
        {
            // Engine owns WinMain, the runtime loop and engine services. A game
            // target supplies CreateApplication and links this library.
            conf.Output = Configuration.OutputType.Lib;
            conf.Defines.Add("WITH_EDITOR=0");
            conf.Defines.Add("NOMINMAX", "WIN32_LEAN_AND_MEAN");
            conf.Options.Add(Options.Vc.Compiler.Exceptions.Enable);
            conf.SourceFilesBuildExcludeRegex.Add(@"[\\/]Editor[\\/]");
            if (target.IsShipping)
            {
                conf.Defines.Add("MORTARENGINE_SHIPPING=1");
                conf.ExportDefines.Add("MORTARENGINE_SHIPPING=1");
            }
        }
    }
}
