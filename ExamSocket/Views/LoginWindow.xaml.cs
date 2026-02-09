using ExamSocket.Services;
using ExamSocket.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ExamSocket.Views
{
    /// <summary>
    /// Login.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();

            // DataContext가 설정된 후 이벤트 연결을 위해 Loaded 이벤트 사용
            Loaded += LoginWindow_Loaded;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel viewModel)
            {
                // 로그인 성공 시 창 닫기
                viewModel.LoginCompleted += (isSuccess, message) =>
                {
                    if (isSuccess)
                    {
                        this.DialogResult = true; // 창 닫기
                    }
                    else
                    {
                        MessageBox.Show(message, "로그인에 실패했습니다.");
                    }
                };
                // 회원가입 버튼 클릭 시 회원가입 모달 띄우기
                viewModel.OpenSignupRequested += () =>
                {
                    SignupWindow signupWindow = new SignupWindow();
                    // ViewModel 생성 및 주입
                    SignupViewModel signupViewModel = new SignupViewModel(new AuthService());
                    signupWindow.DataContext = signupViewModel;
                    signupWindow.ShowDialog();
                };
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel viewModel)
            {
                // PasswordBox의 암호 데이터를 ViewModel로 전달
                if(sender is PasswordBox passwordBox)
                {
                    viewModel.Password = passwordBox.Password;
                }
            }
        }
    }
}
