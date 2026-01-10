using System;
using Microsoft.AspNetCore.Components;

namespace ACTypeBrowser.Services;

public class PageTitleService
{
    public string? Title { get; private set; }
    public RenderFragment? TitleFragment { get; private set; }
    public event Action? OnTitleChanged;

    public void SetTitle(string? title)
    {
        Title = title;
        TitleFragment = null;
        OnTitleChanged?.Invoke();
    }

    public void SetTitle(RenderFragment fragment)
    {
        Title = null;
        TitleFragment = fragment;
        OnTitleChanged?.Invoke();
    }
}
