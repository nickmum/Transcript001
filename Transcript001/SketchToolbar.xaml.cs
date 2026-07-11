using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Transcript001
{
    public partial class SketchToolbar : UserControl
    {
        private enum SketchTool { Pencil, Brush, Highlighter, Eraser }

        private SketchTool _currentTool = SketchTool.Pencil;
        private Color _currentColor = Colors.Black;
        private double _currentSize = 2;

        // The canvas this toolbar controls; set by whoever creates the sketch tab.
        public SketchCanvas TargetCanvas { get; set; }

        public SketchToolbar()
        {
            InitializeComponent();
        }

        private void PencilButton_Click(object sender, RoutedEventArgs e)
        {
            _currentTool = SketchTool.Pencil;
            ApplySettings();
        }

        private void BrushButton_Click(object sender, RoutedEventArgs e)
        {
            _currentTool = SketchTool.Brush;
            ApplySettings();
        }

        private void HighlighterButton_Click(object sender, RoutedEventArgs e)
        {
            _currentTool = SketchTool.Highlighter;
            ApplySettings();
        }

        private void EraserButton_Click(object sender, RoutedEventArgs e)
        {
            _currentTool = SketchTool.Eraser;
            ApplySettings();
        }

        private void ColorPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as ComboBox)?.SelectedItem is ComboBoxItem item &&
                item.Content is string colorName &&
                colorName != "Custom...")
            {
                _currentColor = (Color)ColorConverter.ConvertFromString(colorName);
                ApplySettings();
            }
        }

        private void BrushSizePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as ComboBox)?.SelectedItem is ComboBoxItem item && item.Content is string sizeName)
            {
                _currentSize = sizeName switch
                {
                    "Medium" => 6.0,
                    "Large" => 12.0,
                    _ => 2.0
                };
                ApplySettings();
            }
        }

        private void ApplySettings()
        {
            if (TargetCanvas == null) return;

            switch (_currentTool)
            {
                case SketchTool.Pencil:
                    TargetCanvas.StrokeBrush = new SolidColorBrush(_currentColor);
                    TargetCanvas.StrokeThickness = _currentSize;
                    break;
                case SketchTool.Brush:
                    TargetCanvas.StrokeBrush = new SolidColorBrush(_currentColor);
                    TargetCanvas.StrokeThickness = _currentSize * 2;
                    break;
                case SketchTool.Highlighter:
                    TargetCanvas.StrokeBrush = new SolidColorBrush(
                        Color.FromArgb(96, _currentColor.R, _currentColor.G, _currentColor.B));
                    TargetCanvas.StrokeThickness = _currentSize * 4;
                    break;
                case SketchTool.Eraser:
                    // The canvas background is white, so painting white erases.
                    TargetCanvas.StrokeBrush = Brushes.White;
                    TargetCanvas.StrokeThickness = _currentSize * 4;
                    break;
            }
        }
    }
}
