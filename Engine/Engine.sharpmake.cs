using Sharpmake;

namespace GameBench
{
    [Generate]
    public class Engine : Project
    {
        public Engine()
        {
            Name = "Engine";
            SourceRootPath = @"[project.SharpmakeCsPath]\Source";

            IsFileNameToLower = false;
            IsTargetFileNameToLower = false;

            AddTargets(new Target(
                Platform.win64,
                DevEnv.vs2026,
                Optimization.Debug | Optimization.Release));
        }

        [Configure]
        public void ConfigureAll(Configuration conf, Target target)
        {
            conf.ProjectFileName = "[project.Name]_[target.DevEnv]_[target.Platform]";
            conf.ProjectPath = @"[project.SharpmakeCsPath]\Intermediate\ProjectFiles";

            conf.IntermediatePath = @"[project.SharpmakeCsPath]\Intermediate\Build\[project.Name]\[target.Platform]_[conf.Name]";
            conf.TargetPath = @"[project.SharpmakeCsPath]\Binaries\[target.Platform]\[conf.Name]";

            conf.PrecompHeader = "stdafx.h";
            conf.PrecompSource = "stdafx.cpp";

            conf.IncludePaths.Add(@"[project.SharpmakeCsPath]\Source\Public");
            conf.IncludePaths.Add(@"[project.SharpmakeCsPath]\Source\Private");

            conf.Options.Add(Options.Vc.General.PlatformToolset.v145);
            conf.Options.Add(Options.Vc.Compiler.CppLanguageStandard.Latest);

            InitializePlatform(conf, target);
            InitializeDependencies(conf, target);
            BuildShaderPipeline(conf);
        }

        void InitializePlatform(Configuration conf, Target target)
        {
            if (target.Platform == Platform.win64)
            {
<<<<<<< HEAD
                conf.Defines.Add("GAMEBENCH_PLATFORM_WINDOWS");
=======
>>>>>>> 376238c ([*/+]Add GLFW as the platform agnostic OS Windowing backend)
                conf.Options.Add(Options.Vc.Linker.SubSystem.Windows);
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

            conf.AddPrivateDependency<DirectX12>(target);
            conf.AddPrivateDependency<ShaderMakeTool>(target);
        }

        void BuildShaderPipeline(Configuration conf)
        {
            /* Compile the HLSL under Engine\Shaders before every build so the runtime
            * shader libraries can load the compiled blobs from next to the executable:
            *   Vulkan : "<TargetDir>\Shaders\SPIRV\<Name>_<Entry>.spirv"
            *   DX12   : "<TargetDir>\Shaders\DXIL\<Name>_<Entry>.dxil"
            */
            conf.EventPreBuildDescription = "Compiling shaders with ShaderMake (SPIR-V + DXIL)";
            conf.EventPreBuild.Add(@"call ""$(SolutionDir)Engine\Shaders\CompileProjectShaders.bat"" nopause ""$(TargetDir)Shaders"" ""$(Configuration)"" ""$(SolutionDir)Engine\ThirdParty\ShaderMake\Binaries\win64_$(Configuration)\ShaderMake.exe"" ""$(VULKAN_SDK)\Bin\dxc.exe"" SPIRV");
            conf.EventPreBuild.Add(@"call ""$(SolutionDir)Engine\Shaders\CompileProjectShaders.bat"" nopause ""$(TargetDir)Shaders"" ""$(Configuration)"" ""$(SolutionDir)Engine\ThirdParty\ShaderMake\Binaries\win64_$(Configuration)\ShaderMake.exe"" auto DXIL");
        }
    }
}
