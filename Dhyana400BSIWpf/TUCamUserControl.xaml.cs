using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Dhyana400BSIWpf
{
    /// <summary>
    /// TUCamUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class TUCamUserControl : UserControl
    {
        private TUCamViewModel VM;

        public TUCamUserControl()
        {
            InitializeComponent();

            VM=new TUCamViewModel();
            this.DataContext = VM;

            this.Loaded += OnControlLoaded;
            this.Unloaded += OnControlUnloaded;
        }

        private Window? _parentWindow;
        private bool _isEventRegistered = false;
        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            RegisterGlobalClickEvent();
        }

        private void OnControlUnloaded(object sender, RoutedEventArgs e)
        {
            UnregisterGlobalClickEvent();
        }

        private void RegisterGlobalClickEvent()
        {
            if (_isEventRegistered) return;

            // 在父窗口级别注册事件
            _parentWindow = Window.GetWindow(this);
            if (_parentWindow != null)
            {
                _parentWindow.PreviewMouseDown += ParentWindow_PreviewMouseDown;
                _isEventRegistered = true;
            }
        }

        private void UnregisterGlobalClickEvent()
        {
            if (!_isEventRegistered) return;

            if (_parentWindow != null)
            {
                _parentWindow.PreviewMouseDown -= ParentWindow_PreviewMouseDown;
                _parentWindow = null;
            }

            _isEventRegistered = false;
        }

        private void ParentWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not FrameworkElement clickedElement)
                return;

            if (IsDescendantOfTextBox(clickedElement))
                return;

            bool isClickInsideThisControl = IsElementInsideControl(clickedElement, this);

            if (!isClickInsideThisControl)
            {
                var focusedTextBox = GetFocusedTextBoxInControl();
                if (focusedTextBox != null)
                {
                    ProcessTextBoxInput(focusedTextBox);
                }
                return;
            }

            var currentFocusedTextBox = GetFocusedTextBoxInControl();
            if (currentFocusedTextBox != null)
            {
                ProcessTextBoxInput(currentFocusedTextBox);
            }
        }

        private static bool IsElementInsideControl(FrameworkElement element, FrameworkElement control)
        {
            if (element == null || control == null) return false;

            DependencyObject current = element;
            while (current != null)
            {
                if (current == control)
                    return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private TextBox GetFocusedTextBoxInControl()
        {
            var textBoxes = FindVisualChildren<TextBox>(this);
            return textBoxes.FirstOrDefault(tb => tb.IsFocused)!;
        }

        private static bool IsDescendantOfTextBox(FrameworkElement element)
        {
            while (element != null)
            {
                if (element is TextBox)
                    return true;
                element = element.Parent as FrameworkElement;
            }
            return false;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    var child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (var childOfChild in FindVisualChildren<T>(child!))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void ProcessTextBoxInput(TextBox textBox)
        {
            if (textBox == null) return;

            if (!double.TryParse(textBox.Text, out double value))
            {
                // 非法输入，回退原绑定值
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                return;
            }

            // 根据 TextBox 的 Name 或 Tag 应用不同的范围限制
            value = textBox.Name switch
            {
                "BrightnessTextBox" => Math.Clamp(value, 20, 255),
                "ContrastTextBox" => Math.Clamp(value, 0, 255),
                "GammaTextBox" => Math.Clamp(value, 0, 255),
                "ExposureTextBox" => Math.Clamp(value, VM.ExposureRangeMin, VM.ExposureRangeMax),
                "LevelTextBox" => Math.Clamp(value, VM.LevelRangeMin, VM.LevelRangeMax),
                _ => value // 默认不限制
            };

            // 更新文本和绑定
            textBox.Text = value.ToString("F0");
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            Keyboard.ClearFocus();
        }

        private void BrightnessTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (sender is not TextBox textBox) return;

            if (double.TryParse(textBox.Text, out double value))
            {
                value = Math.Clamp(value, 20, 255);//20-255

                textBox.Text = value.ToString("F0");

                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();

                Keyboard.ClearFocus();
            }
            else
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            }

            e.Handled = true;
        }

        private void ContrastTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (sender is not TextBox textBox) return;

            if (double.TryParse(textBox.Text, out double value))
            {
                value = Math.Clamp(value, 0, 255);//0-255

                textBox.Text = value.ToString("F0");

                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();

                Keyboard.ClearFocus();
            }
            else
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            }

            e.Handled = true;
        }

        private void GammaTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (sender is not TextBox textBox) return;

            if (double.TryParse(textBox.Text, out double value))
            {
                value = Math.Clamp(value, 0, 255);//0-255

                textBox.Text = value.ToString("F0");

                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();

                Keyboard.ClearFocus();
            }
            else
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            }

            e.Handled = true;
        }

        private void ExposureTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (sender is not TextBox textBox) return;

            if (double.TryParse(textBox.Text, out double value))
            {
                value = Math.Clamp(value, VM.ExposureRangeMin, VM.ExposureRangeMax);//vm范围

                textBox.Text = value.ToString("F0");

                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();

                Keyboard.ClearFocus();
            }
            else
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            }

            e.Handled = true;
        }

        private void LevelTextbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (sender is not TextBox textBox) return;

            if (double.TryParse(textBox.Text, out double value))
            {
                value = Math.Clamp(value, VM.LevelRangeMin, VM.LevelRangeMax);//vm范围，随bit而不同

                textBox.Text = value.ToString("F0");

                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();

                Keyboard.ClearFocus();
            }
            else
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            }

            e.Handled = true;
        }
    }
}
