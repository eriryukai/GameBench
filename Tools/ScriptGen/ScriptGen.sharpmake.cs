using Sharpmake;

namespace GameBench
{
    [Generate]
    public class ScriptGen : Project
    {
        public ScriptGen() : base(typeof(GameBenchTarget))
        {
            Name = "ScriptGen";
            SourceRootPath = @"[project.SharpmakeCsPath]\Source";

            IsFileNameToLower = false;
            IsTargetFileNameToLower = false;

            AddTargets(new GameBenchTarget());
        }

        [Configure]
        public void ConfigureAll(Configuration conf, GameBenchTarget target)
        {
            conf.ProjectFileName = "[project.Name]_[target.DevEnv]_[target.Platform]";
            conf.ProjectPath = @"[project.SharpmakeCsPath]\Intermediate\ProjectFiles";

            conf.IntermediatePath = @"[project.SharpmakeCsPath]\Intermediate\Build\[project.Name]\[target.Platform]_[conf.Name]";
            conf.TargetPath = @"[project.SharpmakeCsPath]\Binaries\[target.Platform]\[conf.Name]";

            conf.IncludePaths.Add(@"[project.SharpmakeCsPath]\Source\Public");
            conf.IncludePaths.Add(@"[project.SharpmakeCsPath]\Source\Private");

            conf.Options.Add(Options.Vc.General.PlatformToolset.v145);
            conf.Options.Add(Options.Vc.Compiler.CppLanguageStandard.Latest);
        }

        void ConfigureApplication(Configuration conf, GameBenchTarget target)
        {
            conf.Output = Configuration.OutputType.Lib;
            conf.Options.Add(Options.Vc.Compiler.Exceptions.Enable);
        }
    }
}
