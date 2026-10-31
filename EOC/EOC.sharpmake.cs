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

            conf.Defines.Add("MORTARENGINE_PLATFORM_WINDOWS");
            conf.Defines.Add("NOMINMAX", "WIN32_LEAN_AND_MEAN");
            conf.AddPrivateDependency<Engine>(target);

            conf.VcxprojUserFile = new Configuration.VcxprojUserFileSettings
            {
                LocalDebuggerCommand = "$(TargetPath)",
                LocalDebuggerWorkingDirectory = "$(TargetDir)"
            };
        }
    }
}
