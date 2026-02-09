using ExamSocket.Services;
using ExamSocket.ViewModels;
using ExamSocket.Views;
using System.Windows;

namespace ExamSocket
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            LoginViewModel loginViewModel = new LoginViewModel(new AuthService());
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.DataContext = loginViewModel;

            // 로그인 창을 modal로 표시
            // TODO: 모달로 구현 후 UserControl로 구현하는 방식 적용
            bool? loginResult = loginWindow.ShowDialog();

            // 로그인 성공시 메인 윈도우 실행
            if (loginResult == true)
            {
                long loggedInUserId = loginViewModel.LoggedInUserId;
                ChatViewModel chatViewModel = new ChatViewModel(loggedInUserId);

                MainWindow mainWindow = new MainWindow();
                mainWindow.DataContext = chatViewModel;
                MainWindow = mainWindow;
                mainWindow.Show();

                ShutdownMode = ShutdownMode.OnMainWindowClose;
            } 
            else
            {
                Shutdown();
            }
        }
    }
}
