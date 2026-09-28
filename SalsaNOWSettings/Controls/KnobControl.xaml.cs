using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace SalsaNOWSettings.Controls;

public sealed partial class KnobControl : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(KnobControl),
            new PropertyMetadata(40d, OnValueChanged));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(KnobControl), new PropertyMetadata(0d));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(KnobControl), new PropertyMetadata(100d));

    public event EventHandler<double>? ValueChanged;

    private bool _dragging;

    public KnobControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisual();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KnobControl knob)
        {
            knob.UpdateVisual();
            knob.ValueChanged?.Invoke(knob, knob.Value);
        }
    }

    private void UpdateVisual()
    {
        var range = Math.Max(0.0001, Maximum - Minimum);
        var normalized = (Value - Minimum) / range;
        IndicatorRotate.Angle = -135 + (normalized * 270);
        ValueLabel.Text = Math.Round(Value).ToString();
    }

    private void HitArea_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _dragging = true;
        HitArea.CapturePointer(e.Pointer);
        ApplyPointer(e.GetCurrentPoint(HitArea).Position);
    }

    private void HitArea_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging)
        {
            ApplyPointer(e.GetCurrentPoint(HitArea).Position);
        }
    }

    private void HitArea_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _dragging = false;
        HitArea.ReleasePointerCapture(e.Pointer);
    }

    private void ApplyPointer(Point point)
    {
        var cx = ActualWidth / 2;
        var cy = ActualHeight / 2;
        var angle = Math.Atan2(point.Y - cy, point.X - cx) * 180 / Math.PI;
        var swept = angle + 135;
        if (swept < 0)
        {
            swept += 360;
        }

        swept = Math.Clamp(swept, 0, 270);
        var normalized = swept / 270;
        Value = Minimum + (normalized * (Maximum - Minimum));
    }
}
