using Sharpmake;

namespace Mortar
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

            conf.Options.Add(Options.Vc.General.PlatformToolset.v145);
            conf.Options.Add(Options.Vc.Compiler.CppLanguageStandard.Latest);
        }
    }
}
