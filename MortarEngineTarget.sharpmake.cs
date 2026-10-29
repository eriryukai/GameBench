using System;
using Sharpmake;

namespace MortarEngine
{
    public class MortarEngineTarget : Target
    {
        public MortarEngineTarget()
            : base(Platform.win64, DevEnv.vs2026, Optimization.Debug | Optimization.Release | Optimization.Retail)
        {
        }

        public bool IsShipping => Optimization == Optimization.Retail;
        public override string Name => IsShipping ? "Shipping" : Optimization.ToString();
        public string OutputFolder => Name.ToLowerInvariant().Replace(' ', '_');
        public MortarEngineTarget NativeToolTarget => (MortarEngineTarget)Clone();
        public MortarEngineTarget ManagedTarget => (MortarEngineTarget)Clone(DotNetFramework.net10_0);
    }
}
