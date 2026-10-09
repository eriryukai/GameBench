#nullable enable
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AvaloniaSplashScreenDemo.ViewModels;
using System;

namespace AvaloniaSplashScreenDemo
{
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? data)
        {
            var name = data!.GetType().FullName!.Replace("ViewModel", "View");
            var type = Type.GetType(name);

            return type != null
                ? (Control)Activator.CreateInstance(type)!
                : new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}