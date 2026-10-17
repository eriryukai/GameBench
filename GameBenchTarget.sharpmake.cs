using System;
using Sharpmake;

namespace GameBench
{
    public class GameBenchTarget : Target
    {
        public GameBenchTarget()
            : base(Platform.win64, DevEnv.vs2026, Optimization.Debug | Optimization.Release | Optimization.Retail)
        {
        }

        public bool IsShipping => Optimization == Optimization.Retail;
        public override string Name => IsShipping ? "Shipping" : Optimization.ToString();
        public string OutputFolder => Name.ToLowerInvariant().Replace(' ', '_');
        public GameBenchTarget NativeToolTarget => (GameBenchTarget)Clone();
        public GameBenchTarget ManagedTarget => (GameBenchTarget)Clone(DotNetFramework.net10_0);
    }
}
