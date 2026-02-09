using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExamSocket.Services;
using System.Diagnostics;

namespace ExamSocket.ViewModels
{
    public partial class SignupViewModel : ObservableObject
    {
        private readonly IAuthService _authService;

        public SignupViewModel(IAuthService authService)
        {
            _authService = authService;
        }

        [ObservableProperty]
        private string id = string.Empty;
        [ObservableProperty]
        private string userName = string.Empty;
        [ObservableProperty]
        private string password = string.Empty;

        public event Action<bool, string>? RegisterCompleted; // 회원가입 성공 여부
        public event Action? CloseDialog; // 취소로 창 닫기

        [RelayCommand]
        private void Register()
        {
            Debug.WriteLine("회원가입 시작..");

            // 1. 유효성 검사
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Password))
            {
                RegisterCompleted?.Invoke(false, "아이디 또는 비밀번호를 입력해주세요.");
                return;
            }

            try
            {
                // 2. 비밀번호 암호화 (Salt 적용)
                // 결과로 Hash값과 Salt값 두 개를 돌려받음.
                var (hashedPassword, salt) = PasswordHasher.CreateHash(Password);

                bool isSuccess = _authService.Register(Id, hashedPassword, salt, UserName);

                // 3. 결과 알림
                if (isSuccess)
                {
                    Debug.WriteLine($"[DB 저장 완료]");
                    Debug.WriteLine($"ID: {Id}");
                    Debug.WriteLine($"Hash: {hashedPassword}");
                    Debug.WriteLine($"Salt: {salt}");

                    RegisterCompleted?.Invoke(true, "회원가입 성공");
                }
                else
                {
                    RegisterCompleted?.Invoke(false, "이미 등록된 회원입니다.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"오류 발생: {ex.Message}");
                RegisterCompleted?.Invoke(false, "회원가입 중 오류가 발생했습니다.");
            }
        }

        [RelayCommand]
        private void Close()
        {
            CloseDialog?.Invoke();
        }
    }
}
