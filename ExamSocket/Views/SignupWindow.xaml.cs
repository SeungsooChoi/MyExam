using ExamSocket.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ExamSocket.Views
{
    /// <summary>
    /// SignupWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SignupWindow : Window
    {
        public SignupWindow()
        {
            InitializeComponent();
            Loaded += SignupWindow_Loaded;
        }

        private void SignupWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 가입 성공 시 창 닫기 요청 처리
            if(DataContext is SignupViewModel signupViewModel)
            {
                // 회원가입 성공 시
                signupViewModel.RegisterCompleted += (isSuccess, message) =>
                {
                    if (isSuccess)
                    {
                        MessageBox.Show(message, "회원가입 되었습니다. 로그인하세요");
                        this.DialogResult = false;
                    }
                    else
                    {
                        MessageBox.Show(message, "회원가입에 실패했습니다.");
                    }
                };
                // 취소 누를 경우 창 닫기
                signupViewModel.CloseDialog += () =>
                {
                    this.DialogResult = false;
                };
            }
        }

        private void SignupPwBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            // 비밀번호 전달
            if (DataContext is SignupViewModel signupViewModel)
            {
                // PasswordBox의 암호 데이터를 ViewModel로 전달
                if (sender is PasswordBox passwordBox)
                {
                    signupViewModel.Password = passwordBox.Password;
                }
            }
        }
    }
}
