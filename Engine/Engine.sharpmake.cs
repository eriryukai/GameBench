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
        }

        void InitializePlatform(Configuration conf, Target target)
        {
            if (target.Platform == Platform.win64)
            {
                conf.Defines.Add("GAMEBENCH_PLATFORM_WINDOWS");
                conf.Options.Add(Options.Vc.Linker.SubSystem.Windows);
            }
        }
    }
}
