using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Transcript001
{
    public partial class SketchCanvas : UserControl
    {
        private Polyline currentLine;
        private bool isDrawing;

        public SketchCanvas()
        {
            InitializeComponent();
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                isDrawing = true;
                currentLine = new Polyline
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round
                };
                currentLine.Points.Add(e.GetPosition(DrawingCanvas));
                DrawingCanvas.Children.Add(currentLine);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing && currentLine != null)
            {
                currentLine.Points.Add(e.GetPosition(DrawingCanvas));
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDrawing = false;
        }
    }
}
