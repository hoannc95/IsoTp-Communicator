using System.Windows;

namespace IsoTpCommunicator.CustomProps
{
    public static class VisualProps
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.RegisterAttached(
                "CornerRadius",
                typeof(double),
                typeof(VisualProps),
                new PropertyMetadata(5.0)
            );

        public static void SetCornerRadius(UIElement element, double value) => element.SetValue(CornerRadiusProperty, value);
        public static double GetCornerRadius(UIElement element) => (double)element.GetValue(CornerRadiusProperty);
    }
}
