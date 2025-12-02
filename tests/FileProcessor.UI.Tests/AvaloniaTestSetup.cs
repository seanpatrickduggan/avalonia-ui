using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(FileProcessor.UI.Tests.HeadlessAppBuilder))]

namespace FileProcessor.UI.Tests;

public static class HeadlessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<FileProcessor.UI.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
