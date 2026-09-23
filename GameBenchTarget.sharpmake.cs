using System;
using Sharpmake;

namespace GameBench
{
    [Fragment, Flags]
    public enum BuildMode
    {
        Standalone = 1,
        Editor = 2
    }

    public class GameBenchTarget : Target
    {
        public BuildMode Mode;

        public GameBenchTarget() : this(BuildMode.Standalone | BuildMode.Editor) { }

        public GameBenchTarget(BuildMode mode)
            : base(Platform.win64, DevEnv.vs2026, Optimization.Debug | Optimization.Release)
        {
            Mode = mode;
        }

        public override string Name => Optimization + (Mode == BuildMode.Editor ? " Editor" : "");
        public string OutputFolder => Name.ToLowerInvariant().Replace(' ', '_');
        public Target NativeToolTarget => new Target(Platform, DevEnv, Optimization);
        public GameBenchTarget ManagedTarget => (GameBenchTarget)Clone(DotNetFramework.net10_0);
    }
}
