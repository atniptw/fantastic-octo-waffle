namespace BlazorApp.Services;

public sealed class ModalStateService
{
    public bool IsOpen { get; private set; }
    public event Action? OnChange;

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        OnChange?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        OnChange?.Invoke();
    }
}
