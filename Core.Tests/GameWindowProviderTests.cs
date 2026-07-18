using Core.src.Windows;
using System.Drawing;

namespace Core.Tests;

[Trait("Category", "Integration")]
public class GameWindowProviderTests
{
    // These require a running RuneLite process. Skip them with:
    // dotnet test --filter Category!=Integration

    [Fact]
    public void AttachToRunelite()
    {
        // Arrange
        GameWindowProvider provider = new();

        // Act
        provider.Attach();

        // Assert
        Assert.True(provider.IsAttached);
    }

    [Fact]
    public void GetCanvasRect_WhenAttached_ReturnsNonzeroWidthAndHeight()
    {
        // Arrange
        GameWindowProvider provider = new();

        // Act
        provider.Attach();
        var rect = provider.GetCanvasRect();

        // Assert
        Assert.True(rect != Rectangle.Empty);
    }


}
