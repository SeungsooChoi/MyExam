using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExamSocket.Services;
using System.Diagnostics;

namespace ExamSocket.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;

        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;
        }

        [ObservableProperty]
        private string userId = string.Empty;
        [ObservableProperty]
        private string password = string.Empty;
        [ObservableProperty]
        private long loggedInUserId;

        public event Action<bool, string>? LoginCompleted; // 로그인 성공 여부
        public event Action? OpenSignupRequested; // 회원가입 창 열기 요청

        [RelayCommand]
        private void Login()
        {
            Debug.WriteLine("로그인 시작..");

            // 1. 유효성 검사
            if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(Password))
            {
                LoginCompleted?.Invoke(false, "아이디 또는 비밀번호를 입력해주세요.");
                return;
            }

            // 2. DB 검사
            var (isSuccess, message, userNo) = _authService.Login(UserId, Password);

            // 3. 결과 알림
            if (isSuccess)
            {
                LoggedInUserId = userNo;
                LoginCompleted?.Invoke(true, message);
            }
            else
            {
                LoginCompleted?.Invoke(false, message);
            }
        }

        [RelayCommand]
        private void OpenSignup()
        {
            OpenSignupRequested?.Invoke();
        }
    }
}
