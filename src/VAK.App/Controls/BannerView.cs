using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using VAK.Core.Message;

namespace VAK.App.Controls;

/// <summary>Shows a <see cref="BannerSpec"/> with its animation, colour and finish. Used on the real notch and in the Message page preview.</summary>
public sealed class BannerView : Grid
{
    private readonly TextBlock _text;
    private readonly TranslateTransform _move = new();
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly DispatcherTimer _typeTimer = new();
    private readonly DispatcherTimer _lifetime = new();
    private Action? _done;

    public BannerView()
    {
        ClipToBounds = true;
        _text = new TextBlock
        {
            FontFamily = (FontFamily)Application.Current.FindResource("Font.Ui"), FontSize = 13, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.None,
            RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new TransformGroup { Children = { _scale, _move } },
        };
        Children.Add(_text);
        _lifetime.Tick += (_, _) => Finish();
    }

    /// <summary>How wide the notch should be to show this banner comfortably.</summary>
    public static double PreferredWidth(BannerSpec spec)
    {
        if (spec.Animation == BannerAnimation.Scroll) return 320;
        var ft = new FormattedText(spec.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface((FontFamily)Application.Current.FindResource("Font.Ui"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            13, Brushes.White, 1.0);
        return Math.Clamp(ft.Width + 56, 200, 560);
    }

    public void Play(BannerSpec spec, Action? done = null)
    {
        Stop();
        _done = done;
        _text.Text = spec.Animation == BannerAnimation.TypeOn ? "" : spec.Text;
        _text.Foreground = BrushFor(spec);
        _text.Opacity = 1;
        _text.Effect = null;
        _move.X = 0; _scale.ScaleX = _scale.ScaleY = 1;
        _text.HorizontalAlignment = HorizontalAlignment.Center;

        var k = 1 / spec.Speed; // slower speed means longer animations
        TimeSpan T(double ms) => TimeSpan.FromMilliseconds(ms * k);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        switch (spec.Animation)
        {
            case BannerAnimation.Slide:
                _move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(70, 0, T(380)) { EasingFunction = ease });
                _text.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, T(380)));
                break;
            case BannerAnimation.Fade:
                _text.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, T(600)));
                break;
            case BannerAnimation.TypeOn:
                var i = 0;
                _typeTimer.Interval = T(55);
                _typeTimer.Tick -= TypeTick;
                _typeTimer.Tick += TypeTick;
                void TypeTick(object? s, EventArgs e)
                {
                    _text.Text = spec.Text[..Math.Min(++i, spec.Text.Length)];
                    if (i >= spec.Text.Length) _typeTimer.Stop();
                }
                _typeTimer.Start();
                break;
            case BannerAnimation.Scroll:
                _text.HorizontalAlignment = HorizontalAlignment.Left;
                var w = PreferredWidth(spec);
                var tw = Math.Max(w, spec.Text.Length * 8.0);
                _move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(w, -tw, T(Math.Max(4000, tw * 14))) { RepeatBehavior = RepeatBehavior.Forever });
                break;
            case BannerAnimation.Blur:
                var blur = new BlurEffect { Radius = 14 };
                _text.Effect = blur;
                blur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(14, 0, T(650)) { EasingFunction = ease });
                _text.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, T(650)));
                break;
            case BannerAnimation.Pulse:
                _text.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.35, T(700)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
                _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 1.07, T(700)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
                _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.07, T(700)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
                break;
        }

        if (!spec.Repeat)
        {
            _lifetime.Interval = TimeSpan.FromSeconds(spec.DurationSeconds);
            _lifetime.Start();
        }
    }

    private void Finish()
    {
        _lifetime.Stop();
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
        fade.Completed += (_, _) => { var d = _done; Stop(); d?.Invoke(); };
        _text.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>Stops everything and clears the text.</summary>
    public void Stop()
    {
        _lifetime.Stop(); _typeTimer.Stop();
        _text.BeginAnimation(OpacityProperty, null);
        _move.BeginAnimation(TranslateTransform.XProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _text.Effect = null;
        _text.Text = "";
        _text.Opacity = 1;
        _move.X = 0; _scale.ScaleX = _scale.ScaleY = 1;
        _done = null;
    }

    private static Brush BrushFor(BannerSpec spec)
    {
        var color = (Color)ColorConverter.ConvertFromString(spec.ColorHex);
        LinearGradientBrush G(params Color[] stops)
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            for (var i = 0; i < stops.Length; i++) b.GradientStops.Add(new GradientStop(stops[i], i / (double)(stops.Length - 1)));
            return b;
        }
        return spec.Finish switch
        {
            BannerFinish.Silver => G(Colors.White, Color.FromRgb(0x9A, 0xA0, 0xA6), Colors.White),
            BannerFinish.Ocean => G(Color.FromRgb(0x22, 0xD3, 0xEE), Color.FromRgb(0x3B, 0x82, 0xF6)),
            BannerFinish.Aurora => G(Color.FromRgb(0xA8, 0x55, 0xF7), Color.FromRgb(0xF4, 0x3F, 0x6E), Color.FromRgb(0x4F, 0xB8, 0xFF)),
            _ => new SolidColorBrush(color),
        };
    }
}
