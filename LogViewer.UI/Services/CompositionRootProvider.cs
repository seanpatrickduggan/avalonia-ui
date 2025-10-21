using System;
using Microsoft.Extensions.DependencyInjection;

namespace LogViewer.UI.Services;

internal static class CompositionRootProvider
{
    public static IServiceProvider? ServiceProvider { get; set; }
}
