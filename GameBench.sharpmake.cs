// Copyright (c) CreationArt. All Rights Reserved.

using Sharpmake;

[module: Sharpmake.Include(@"GameBenchTarget.sharpmake.cs")]
[module: Sharpmake.Include(@"Engine\Engine.sharpmake.cs")]
[module: Sharpmake.Include(@"Editor\Editor.sharpmake.cs")]
[module: Sharpmake.Include(@"Engine\ThirdParty\ThirdParty.sharpmake.cs")]

namespace GameBench
{
    [Generate]
    public class GameBench : Solution
    {
        public GameBench() : base(typeof(GameBenchTarget))
        {
            Name = "GameBench";
            IsFileNameToLower = false;

            AddTargets(new GameBenchTarget());
        }

        [Configure]
        public void ConfigureAll(Configuration conf, GameBenchTarget target)
        {
            conf.SolutionFileName = "[solution.Name]_[target.DevEnv]_[target.Platform]";
            conf.SolutionPath = @"[solution.SharpmakeCsPath]";

            conf.AddProject<Engine>(target);
            if (target.Mode == BuildMode.Editor)
            {
                conf.AddProject<Editor>(target.ManagedTarget);
                conf.SetStartupProject<Editor>();
            }
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
