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

        public Brush StrokeBrush { get; set; } = Brushes.Black;
        public double StrokeThickness { get; set; } = 2;

        public SketchCanvas()
        {
            InitializeComponent();
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                isDrawing = true;
                DrawingCanvas.CaptureMouse();
                currentLine = new Polyline
                {
                    Stroke = StrokeBrush,
                    StrokeThickness = StrokeThickness,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
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
            currentLine = null;
            DrawingCanvas.ReleaseMouseCapture();
        }
    }
}
