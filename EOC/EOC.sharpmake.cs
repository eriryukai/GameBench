using Sharpmake;

namespace MortarEngine
{
    [Generate]
    public class EOC : Project
    {
        public EOC() : base(typeof(MortarEngineTarget))
        {
            Name = "EOC";
            SourceRootPath = @"[project.SharpmakeCsPath]\Source";

            IsFileNameToLower = false;
            IsTargetFileNameToLower = false;

            AddTargets(new MortarEngineTarget());
        }

        [Configure]
        public void ConfigureAll(Configuration conf, MortarEngineTarget target)
        {
            conf.SolutionFolder = "Games";
            conf.ProjectFileName = "[project.Name]_[target.DevEnv]_[target.Platform]";
            conf.ProjectPath = @"[project.SharpmakeCsPath]\Intermediate\ProjectFiles";

            conf.IntermediatePath = @"[project.SharpmakeCsPath]\Intermediate\Build\[project.Name]\[target.Platform]_[conf.Name]";
            conf.TargetPath = @"[project.SharpmakeCsPath]\Binaries\[target.Platform]\[conf.Name]";

            conf.Output = Configuration.OutputType.Exe;
            conf.Options.Add(Options.Vc.General.PlatformToolset.v145);
            conf.Options.Add(Options.Vc.Compiler.CppLanguageStandard.Latest);
            conf.Options.Add(Options.Vc.Compiler.Exceptions.Enable);
            conf.Options.Add(Options.Vc.Linker.SubSystem.Windows);

            conf.Defines.Add("MORTARENGINE_PLATFORM_WINDOWS");
            conf.Defines.Add("WITH_EDITOR=0");
            conf.Defines.Add("NOMINMAX", "WIN32_LEAN_AND_MEAN");
            if (target.IsShipping)
            {
                conf.Defines.Add("MORTARENGINE_SHIPPING=1");
            }

            // Engine owns WinMain and the runtime loop. EOC supplies game framework
            // classes and the application factory, but no process entry point.
            conf.AddPrivateDependency<Engine>(target);
            conf.AddPrivateDependency<ShaderMakeTool>(target, DependencySetting.OnlyBuildOrder);

            BuildShaderPipeline(conf);

            conf.VcxprojUserFile = new Configuration.VcxprojUserFileSettings
            {
                LocalDebuggerCommand = "$(TargetPath)",
                LocalDebuggerWorkingDirectory = "$(TargetDir)"
            };
        }

        void BuildShaderPipeline(Configuration conf)
        {
            /* Compile the HLSL under Engine\Shaders before every game build so the runtime
            * shader libraries can load the compiled blobs from next to the executable:
            *   Vulkan : "<TargetDir>\Shaders\SPIRV\<Name>_<Entry>.spirv"
            *   DX12   : "<TargetDir>\Shaders\DXIL\<Name>_<Entry>.dxil"
            */
            conf.EventPreBuildDescription = "Compiling shaders with ShaderMake (SPIR-V + DXIL)";
            conf.EventPreBuild.Add(@"call ""$(SolutionDir)Engine\Shaders\CompileProjectShaders.bat"" nopause ""$(TargetDir)Shaders"" ""[target.Name]"" ""$(SolutionDir)Engine\ThirdParty\ShaderMake\Binaries\win64_[target.Name]\ShaderMake.exe"" vulkan SPIRV");
            conf.EventPreBuild.Add(@"call ""$(SolutionDir)Engine\Shaders\CompileProjectShaders.bat"" nopause ""$(TargetDir)Shaders"" ""[target.Name]"" ""$(SolutionDir)Engine\ThirdParty\ShaderMake\Binaries\win64_[target.Name]\ShaderMake.exe"" auto DXIL");
        }
    }
}
