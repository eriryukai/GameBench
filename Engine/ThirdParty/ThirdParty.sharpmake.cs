using Sharpmake;
using System;
using System.IO;
using GameBench;

[Export]
public class DirectX12 : Project
{
    public DirectX12()
        : base(typeof(GameBenchTarget))
    {
        Name = "DirectX12";

        SourceFilesExtensions.Clear();
        IsFileNameToLower = false;
        IsTargetFileNameToLower = false;

        AddTargets(new GameBenchTarget());
    }

    [Configure]
    public void ConfigureAll(Configuration conf, Target target)
    {
        conf.IncludePaths.Add(@"[project.SharpmakeCsPath]\..\..\packages\Microsoft.Direct3D.D3D12.1.619.5\build\native\include");

        conf.LibraryFiles.Add("d3d12.lib");
        conf.LibraryFiles.Add("dxgi.lib");
        conf.LibraryFiles.Add("dxguid.lib");
        conf.LibraryFiles.Add("d3dcompiler.lib");
    }
}

[Generate]
public class ShaderMakeTool : Project
{
    public ShaderMakeTool()
        : base(typeof(GameBenchTarget))
    {
        Name = "ShaderMake";
        SourceRootPath = @"[project.SharpmakeCsPath]\ShaderMake\ShaderMake";
        SourceFilesExtensions.Clear();
        SourceFiles.AddRange(new[]
        {
            @"argparse.h", @"argparse.c", @"ShaderBlob.h", @"ShaderBlob.cpp", @"ShaderMake.cpp"
        });
        IsFileNameToLower = false;
        IsTargetFileNameToLower = false;
        AddTargets(new GameBenchTarget());
    }

    [Configure]
    public void ConfigureAll(Configuration conf, Target target)
    {
        conf.ProjectFileName = "[project.Name]";
        conf.ProjectPath = @"[project.SharpmakeCsPath]\ShaderMake\Intermediate\ProjectFiles";
        conf.IntermediatePath = @"[project.SharpmakeCsPath]\ShaderMake\Intermediate\[target.Platform]_[conf.Name]";
        conf.TargetPath = @"[project.SharpmakeCsPath]\ShaderMake\Binaries\[target.Platform]_[conf.Name]";
        conf.Output = Configuration.OutputType.Exe;
        conf.Options.Add(Options.Vc.Compiler.CppLanguageStandard.Latest);
        conf.Options.Add(Options.Vc.Compiler.Exceptions.Enable);
        conf.Defines.Add("WIN32_LEAN_AND_MEAN");
        conf.Defines.Add("NOMINMAX");
        conf.Defines.Add("_CRT_SECURE_NO_WARNINGS");
    }
}

[Generate]
public class ImGui : Project
{
    public ImGui()
    {
        Name = "ImGui";
        SourceRootPath = @"[project.SharpmakeCsPath]\imgui";
        SourceFilesExtensions.Clear();
        SourceFiles.AddRange(new[]
        {
            @"imgui.cpp", @"imgui.h",
            @"imgui_demo.cpp",
            @"imgui_draw.cpp",
            @"imgui_tables.cpp",
            @"imgui_widgets.cpp",
            @"imconfig.h",
            @"imgui_internal.h",
            @"backends\imgui_impl_win32.cpp", @"backends\imgui_impl_win32.h",
            @"backends\imgui_impl_dx12.cpp", @"backends\imgui_impl_dx12.h"
        });
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
        conf.ProjectFileName = "[project.Name]";
        conf.ProjectPath = @"[project.SharpmakeCsPath]\imgui\Intermediate\ProjectFiles";
        conf.IntermediatePath = @"[project.SharpmakeCsPath]\imgui\Intermediate\[target.Platform]_[conf.Name]";
        conf.TargetPath = @"[project.SharpmakeCsPath]\imgui\Binaries\[target.Platform]_[conf.Name]";
        conf.Output = Configuration.OutputType.Lib;

        conf.IncludePaths.Add(@"[project.SharpmakeCsPath]\imgui");
        conf.IncludePaths.Add(@"[project.SharpmakeCsPath]\imgui\backends");

        conf.Options.Add(Options.Vc.Compiler.CppLanguageStandard.Latest);
        conf.Options.Add(Options.Vc.Compiler.Exceptions.Enable);
        conf.AdditionalCompilerOptions.Add("/utf-8");
        conf.Defines.Add("WIN32_LEAN_AND_MEAN");
        conf.Defines.Add("NOMINMAX");

        conf.AddPrivateDependency<DirectX12>(target);
    }
}