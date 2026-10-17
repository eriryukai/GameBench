// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using Avalonia.Controls;

namespace GameBench.Editor.Managed.UI.MenuBar;

public partial class MainMenu : UserControl
{
    public MainMenu()
    {
        InitializeComponent();

        var exit = this.FindControl<MenuItem>("ExitMenuItem")!;
        exit.Click += (_, _) => (TopLevel.GetTopLevel(this) as Window)?.Close();
    }
}
