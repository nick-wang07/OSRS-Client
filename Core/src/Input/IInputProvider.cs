using System.Drawing;

namespace Core.src.Input;

public interface IInputProvider
{
    Task MoveMouse(Point destination, double gravity = 9, double wind = 3, double maxStep = 30, double targetArea = 12);

    Task Click();

    Task RightClick();

    Task Scroll(bool up, int clicks = 35);

    Task MoveAndClick(Point destination);

    Task DragAndDrop(Point start, Point end, double speed = 25);
}
