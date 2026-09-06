// Copyright (c) CreationArt. All Rights Reserved.

using Sharpmake;

[module: Sharpmake.Include(@"Engine\Engine.sharpmake.cs")]
[module: Sharpmake.Include(@"Engine\ThirdParty\ThirdParty.sharpmake.cs")]

namespace GameBench
{
    [Generate]
    public class GameBench : Solution
    {
        public GameBench()
        {
            Name = "GameBench";
            IsFileNameToLower = false;

            AddTargets(new Target(
                Platform.win64,
                DevEnv.vs2026,
                Optimization.Debug | Optimization.Release));
        }

        [Configure]
        public void ConfigureAll(Configuration conf, Target target)
        {
            conf.SolutionFileName = "[solution.Name]_[target.DevEnv]_[target.Platform]";
            conf.SolutionPath = @"[solution.SharpmakeCsPath]";

            conf.AddProject<Engine>(target);
        }
    }

    public static class Main
    {
        [Sharpmake.Main]
        public static void SharpmakeMain(Sharpmake.Arguments arguments)
        {
            KitsRootPaths.SetUseKitsRootForDevEnv(
                DevEnv.vs2026,
                KitsRootEnum.KitsRoot10,
                Options.Vc.General.WindowsTargetPlatformVersion.v10_0_26100_0);

            arguments.Generate<GameBench>();
        }
    }
}
