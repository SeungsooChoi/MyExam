using System.Security.Cryptography;
using System.Text;

namespace ExamSocket.Services
{
    public class PasswordHasher
    {
        // Salt 크기 (32바이트 = 256비트)
        private const int SaltSize = 32;

        /// <summary>
        /// 비밀번호를 받아서 Salt와 함께 해싱된 결과(Hash)와 사용된 Salt를 반환합니다.
        /// </summary>
        public static (string Hash, string Salt) CreateHash(string password)
        {
            // 1. 무작위 Salt 생성
            byte[] saltBytes = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }

            // 2. 비밀번호와 Salt 결합 후 해싱
            string hash = ComputeHash(password, saltBytes);

            // 3. 나중에 검증을 위해 Salt도 DB에 저장해야 하므로 문자열(Base64)로 변환
            string salt = Convert.ToBase64String(saltBytes);

            return (hash, salt);
        }

        /// <summary>
        /// 실제 해싱을 수행하는 내부 메서드
        /// </summary>
        private static string ComputeHash(string password, byte[] saltBytes)
        {
            using (var sha256 = SHA256.Create())
            {
                // 비밀번호 문자열을 바이트로 변환
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

                // 비밀번호 바이트 + Salt 바이트를 합친 배열 생성
                byte[] combinedBytes = new byte[passwordBytes.Length + saltBytes.Length];
                Array.Copy(passwordBytes, 0, combinedBytes, 0, passwordBytes.Length);
                Array.Copy(saltBytes, 0, combinedBytes, passwordBytes.Length, saltBytes.Length);

                // 해싱 수행
                byte[] hashBytes = sha256.ComputeHash(combinedBytes);

                // 저장하기 편하게 Base64 문자열로 변환하여 반환
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// 입력된 비밀번호가 저장된 Hash/Salt와 일치하는지 확인합니다.
        /// </summary>
        public static bool Verify(string inputPassword, string storedHash, string storedSalt)
        {
            // 1. DB에 저장된 Salt 문자열(Base64)을 다시 바이트 배열로 변환
            byte[] saltBytes = Convert.FromBase64String(storedSalt);

            // 2. 입력된 비밀번호와 가져온 Salt를 사용해 해시값 계산
            string computedHash = ComputeHash(inputPassword, saltBytes);

            // 3. 계산된 해시값과 DB에 저장된 해시값이 같은지 비교
            return computedHash == storedHash;
        }
    }
}
