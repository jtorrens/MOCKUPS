using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Globalization;

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
        _webView.NavigateToString(CreateHtml(svg, geometry, padding, true), new Uri("https://mockups.local/svg-preview/"));
    }

    public void SetMessage(string message)
    {
        _webView.IsVisible = false;
        _message.Text = message;
        _message.IsVisible = true;
    }

    public static string CreateHtml(string svg, SvgReplacementService.Geometry? geometry, double padding, bool showGuides)
    {
        var insetX = geometry is null || geometry.Width <= 0
            ? 0
            : Math.Clamp(padding / geometry.Width * 100, 0, 49);
        var insetY = geometry is null || geometry.Height <= 0
            ? 0
            : Math.Clamp(padding / geometry.Height * 100, 0, 49);
        var aspectWidth = geometry is null || geometry.Width <= 0 ? 1 : geometry.Width;
        var aspectHeight = geometry is null || geometry.Height <= 0 ? 1 : geometry.Height;
        var maxFrameSide = Math.Max(aspectWidth, aspectHeight);
        var frameWidth = Math.Clamp(aspectWidth / maxFrameSide * 94, 1, 94);
        var frameHeight = Math.Clamp(aspectHeight / maxFrameSide * 94, 1, 94);
        var bodyPadding = showGuides ? "8px" : "0";
        var frameBorder = showGuides ? "1px solid rgba(226, 232, 240, .9)" : "0";
        var paddingDisplay = showGuides ? "block" : "none";
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
                  padding: {{bodyPadding}};
                  box-sizing: border-box;
                  color: #f2f6ff;
                }

                .icon-frame {
                  position: relative;
                  width: {{Percent(frameWidth)}};
                  height: {{Percent(frameHeight)}};
                  display: grid;
                  place-items: center;
                  border: {{frameBorder}};
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
                  display: {{paddingDisplay}};
                }

                .icon-frame > svg {
                  display: block;
                  width: 100%;
                  height: 100%;
                  max-width: 100%;
                  max-height: 100%;
                  overflow: visible;
                  color: #000;
                  filter: brightness(0) invert(1);
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

    private static string Percent(double value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture) + "%";
    }
}
