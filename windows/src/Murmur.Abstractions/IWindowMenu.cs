namespace Murmur.Abstractions;

/// <summary>Opens the operating system's keyboard window menu.</summary>
public interface IWindowMenu
{
    /// <summary>Shows the system menu for the supplied native window.</summary>
    void Show(nint handle);
}
