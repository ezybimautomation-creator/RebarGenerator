using System.Windows;

namespace ToolsByGimhan.RebarGenerator
{
    public partial class InputDialog : Window
    {
        public string Value => InputBox.Text;

        public InputDialog(string prompt = "Enter value:", string title = "Input",
                           string defaultValue = "")
        {
            InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            InputBox.Text = defaultValue;
            Loaded += (_, _) => { InputBox.SelectAll(); InputBox.Focus(); };
        }

        private void OkClick    (object sender, RoutedEventArgs e) { DialogResult = true;  Close(); }
        private void CancelClick(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
