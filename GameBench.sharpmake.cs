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

            if (target.Mode == BuildMode.Editor)
            {
                conf.AddProject<Editor>(target.ManagedTarget);
            }

            //@TODO: Currently, disabling engine project as editor right now purely visual. Need to solve:
            //@TODO: Figure out a model, similiar to unreal engine that even with a game project we still have one application. 
            //       With editor, same BUT we will interop the function. Right now, We created 4 functions for interop.
            //       Ideally, we just have ONE interop function and stuff like tick, shutdown etx just handle by editor telling the engine to shut down or tick like giving messages
            //@TODO: Figure out a design. Using UnrealEngine as inspiration, the Engine class acts as the base Application class, and depends on Runtime or Editor, we will have 
            //       EditorEngine or GameEngine
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
