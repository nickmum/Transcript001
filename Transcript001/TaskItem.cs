using System.ComponentModel;

namespace Transcript001
{
    public enum TaskCategory
    {
        Quick,
        Medium,
        Long
    }

    public class TaskItem : INotifyPropertyChanged
    {
        private bool _isCompleted;

        public TaskItem(string text, TaskCategory category)
        {
            Text = text;
            Category = category;
        }

        public string Text { get; }

        public TaskCategory Category { get; }

        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted == value) return;
                _isCompleted = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompleted)));
            }
        }

        public string DurationDisplay
        {
            get
            {
                switch (Category)
                {
                    case TaskCategory.Quick: return "<15m";
                    case TaskCategory.Medium: return "15–60m";
                    default: return ">1h";
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
