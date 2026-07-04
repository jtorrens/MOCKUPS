using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class SvgMarkupPreview : Grid
{
    private readonly NativeWebView _webView;
    private readonly TextBlock _message;

    public SvgMarkupPreview()
    {
        _webView = new NativeWebView
        {
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _message = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.76,
            IsVisible = false,
        };

        Children.Add(_webView);
        Children.Add(_message);
    }

    public void SetSvg(string svg)
    {
        _message.IsVisible = false;
        _webView.IsVisible = true;
        _webView.NavigateToString(Html(svg), new Uri("https://mockups.local/svg-preview/"));
    }

    public void SetMessage(string message)
    {
        _webView.IsVisible = false;
        _message.Text = message;
        _message.IsVisible = true;
    }

    private static string Html(string svg)
    {
        return $$"""
            <!doctype html>
            <html>
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <style>
                html, body {
                  width: 100%;
                  height: 100%;
                  margin: 0;
                  overflow: hidden;
                  background: transparent;
                }

                body {
                  display: grid;
                  place-items: center;
                  padding: 14px;
                  box-sizing: border-box;
                  color: #f2f6ff;
                }

                svg {
                  display: block;
                  max-width: 100%;
                  max-height: 100%;
                  overflow: visible;
                }
              </style>
            </head>
            <body>
              {{svg}}
            </body>
            </html>
            """;
    }
}
