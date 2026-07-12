using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Transcript001
{
    public partial class TaskListPanel : UserControl
    {
        private readonly ObservableCollection<TaskItem> _tasks = new ObservableCollection<TaskItem>();

        public TaskListPanel()
        {
            InitializeComponent();
            TasksItemsControl.ItemsSource = _tasks;
        }

        private TaskCategory SelectedCategory
        {
            get
            {
                if (CategoryComboBox.SelectedItem is ComboBoxItem item &&
                    Enum.TryParse(item.Content as string, out TaskCategory category))
                {
                    return category;
                }
                return TaskCategory.Quick;
            }
        }

        private void AddTask()
        {
            string text = NewTaskTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _tasks.Add(new TaskItem(text, SelectedCategory));
            NewTaskTextBox.Clear();
            NewTaskTextBox.Focus();
        }

        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            AddTask();
        }

        private void NewTaskTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddTask();
                e.Handled = true;
            }
        }

        private void RemoveTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is TaskItem task)
            {
                _tasks.Remove(task);
            }
        }
    }
}
