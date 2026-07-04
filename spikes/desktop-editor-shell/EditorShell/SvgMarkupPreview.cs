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

    public void SetSvg(string svg, SvgReplacementService.Geometry? geometry, double padding)
    {
        _message.IsVisible = false;
        _webView.IsVisible = true;
        _webView.NavigateToString(Html(svg, geometry, padding), new Uri("https://mockups.local/svg-preview/"));
    }

    public void SetMessage(string message)
    {
        _webView.IsVisible = false;
        _message.Text = message;
        _message.IsVisible = true;
    }

    private static string Html(string svg, SvgReplacementService.Geometry? geometry, double padding)
    {
        var insetX = geometry is null || geometry.Width <= 0
            ? 0
            : Math.Clamp(padding / geometry.Width * 100, 0, 49);
        var insetY = geometry is null || geometry.Height <= 0
            ? 0
            : Math.Clamp(padding / geometry.Height * 100, 0, 49);
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
                  padding: 8px;
                  box-sizing: border-box;
                  color: #f2f6ff;
                }

                .icon-frame {
                  position: relative;
                  width: 100%;
                  height: 100%;
                  display: grid;
                  place-items: center;
                  border: 1px solid rgba(226, 232, 240, .9);
                  box-sizing: border-box;
                }

                .padding-frame {
                  position: absolute;
                  left: {{insetX.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}}%;
                  right: {{insetX.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}}%;
                  top: {{insetY.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}}%;
                  bottom: {{insetY.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}}%;
                  border: 1px solid #ff2d55;
                  box-sizing: border-box;
                  pointer-events: none;
                  z-index: 2;
                }

                svg {
                  display: block;
                  width: 92%;
                  height: 92%;
                  overflow: visible;
                }
              </style>
            </head>
            <body>
              <div class="icon-frame">
                {{svg}}
                <div class="padding-frame"></div>
              </div>
            </body>
            </html>
            """;
    }
}
