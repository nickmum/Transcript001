using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections;
using System.Collections.Generic;

namespace Transcript001
{
    public partial class SketchToolbar : UserControl, IEnumerable
    {
        private readonly List<UIElement> _items = new List<UIElement>();

        public SketchToolbar()
        {
            InitializeComponent();
        }

        // Implement IEnumerable interface
        public IEnumerator GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        // Add method to allow collection initializer syntax
        public void Add(UIElement element)
        {
            _items.Add(element);
            ToolbarPanel.Children.Add(element);
        }

        private void PencilButton_Click(object sender, RoutedEventArgs e)
        {
            // Set brush style to pencil. (thin line thickness)
        }

        private void BrushButton_Click(object sender, RoutedEventArgs e)
        {
            // Set brush style to brush. (midium line tthickness)
        }

        private void HighlighterButton_Click(object sender, RoutedEventArgs e)
        {
            // Set brush style to highlighter. (thick line thickness)
        }

        private void EraserButton_Click(object sender, RoutedEventArgs e)
        {
            // Set brush style to eraser
        }

        private void ColorPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Change brush color 
        }

        private void BrushSizePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Change brush size 
        }
    }
}
