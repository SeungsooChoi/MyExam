using ExamSocket.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace ExamSocket
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // App.xaml.cs  ChatViewModel 사용
            if (DataContext is ChatViewModel viewModel)
            {
                // 스크롤 이벤트 구독
                viewModel.ScrollRequested += () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageScrollViewer.ScrollToEnd();
                    });
                };
            }
        }

        // Enter 키로 메시지 전송
        private void MessageInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                if (DataContext is ChatViewModel viewModel)
                {
                    viewModel.SendMessageCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}