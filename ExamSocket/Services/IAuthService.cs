namespace ExamSocket.Services
{
    public interface IAuthService
    {
        (bool isSuccess, string message, long userNo) Login(string id, string password);
        bool Register(string id, string hashPassword, string salt, string username);
    }
}
