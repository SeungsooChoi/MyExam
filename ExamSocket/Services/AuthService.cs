using Microsoft.Data.SqlClient;
using System.Configuration;
using System.Diagnostics;
using System.Windows.Threading;

namespace ExamSocket.Services
{
    class AuthService : IAuthService
    {
        private readonly string _connectionString;
        private DispatcherTimer? _activeTimer;

        public AuthService()
        {
            // App.config에서 DB 연결 설정 가져옴
            _connectionString = ConfigurationManager.ConnectionStrings["MyDbConnection"].ConnectionString;
        }

        public (bool isSuccess, string message, long userNo) Login(string id, string password)
        {
            try
            {
                // Microsoft.Data.SqlClient 객체 사용하여 DB 연결
                using SqlConnection connection = new SqlConnection(_connectionString);
                connection.Open();

                string query = "SELECT id, login_id, username, password_hash, password_salt, last_active_time FROM Users WHERE login_id = @id";

                // DB에서 가져올 데이터를 담을 변수
                string storedHash = string.Empty;
                string storedSalt = string.Empty;
                string userName = string.Empty;
                DateTime? lastActiveTime = null;
                long dbUserNo = 0;
                bool isUserExist = false;

                // 1. 유저 조회
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@id", id);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        // 1. 아이디가 존재하는지 확인
                        if (reader.Read())
                        {
                            isUserExist = true;
                            // 2. DB에서 저장된 값 읽어오기
                            dbUserNo = Convert.ToInt64(reader["id"]);
                            storedHash = reader["password_hash"].ToString()!;
                            storedSalt = reader["password_salt"].ToString()!;
                            userName = reader["username"].ToString()!;

                            if (reader["last_active_time"] != DBNull.Value)
                            {
                                lastActiveTime = Convert.ToDateTime(reader["last_active_time"]);
                            }
                        }
                    }
                }

                if (!isUserExist)
                {
                    return (false, "로그인 실패: 존재하지 않는 아이디입니다.", 0);
                }

                // 2. 비밀번호 검증
                if (PasswordHasher.Verify(password, storedHash, storedSalt))
                {
                    // 3. 중복 로그인 체크
                    // 마지막 활동 시간이 30초 이내라면 접속 중인 것으로 간주한다.
                    if(lastActiveTime != null)
                    {
                        TimeSpan timeSinceLastactive = DateTime.Now - lastActiveTime.Value;
                        if(timeSinceLastactive.TotalSeconds < 30)
                        {
                            return (false, "이미 접속중인 아이디입니다.", 0);
                        }
                    }

                    // 4. 로그인 성공 처리
                    Debug.WriteLine($"로그인 성공: {userName}님 환영합니다.");

                    // last_active_time 갱신
                    UpdateLastActiveTime(id, connection);

                    // 10초마다 last_active_time 갱신
                    StartActiveTimer(id);
                    return (true, "로그인 성공", dbUserNo);
                }
                else
                {
                    // 아이디는 맞지만 비밀번호가 틀린 경우
                    return (false, "로그인 실패: 비밀번호가 일치하지 않습니다.", 0);
                }
            }
            catch (SqlException ex) 
            {
                // TODO: 로깅 작업 수행
                return (false, $"DB 접속 에러: {ex.Message}", 0);
            }
        }

        public bool Register(string id, string hashPassword, string salt, string username)
        {
            // DB를 호출하여 중복 ID 체크 및 저장
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    // 1. 중복 ID 체크
                    string checkQuery = "SELECT COUNT(*) FROM Users WHERE login_id = @id";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@id", id);
                        int count = (int)checkCmd.ExecuteScalar();
                        if (count > 0) return false; // 이미 존재함
                    }

                    // 2. 회원가입 정보 저장
                    string insertQuery = @"INSERT INTO Users (login_id, password_hash, password_salt, username, created_at) 
                                    VALUES (@id, @hash, @salt, @name, GETDATE())";

                    using SqlCommand command = new SqlCommand(insertQuery, connection);
                    command.Parameters.AddWithValue("@id", id);
                    command.Parameters.AddWithValue("@hash", hashPassword);
                    command.Parameters.AddWithValue("@salt", salt);
                    command.Parameters.AddWithValue("@name", username);

                    int result = command.ExecuteNonQuery();
                    return result > 0;
                }
            }
            catch (SqlException ex)
            {
                Debug.WriteLine($"회원가입 에러: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// last_active_time 업데이트 (sqlConnection 재사용)
        /// </summary>
        private void UpdateLastActiveTime(string id, SqlConnection? sqlConnection = null)
        {
            string query = "UPDATE Users SET last_active_time = GETDATE() WHERE login_id = @id";

            // 기존 연결이 있다면 재사용
            if (sqlConnection != null)
            {
                using (SqlCommand command = new SqlCommand(query, sqlConnection))
                {
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();
                }
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void StartActiveTimer(string userId)
        {
            // 기존 타이머가 있다면 중지
            if(_activeTimer != null) _activeTimer.Stop();

            _activeTimer = new DispatcherTimer();
            _activeTimer.Interval = TimeSpan.FromSeconds(10); // 10초마다 갱신
            _activeTimer.Tick += (s, e) =>
            {
                UpdateLastActiveTime(userId); // 타이머는 별도의 이벤트라서 새로 DB연결이 필요?
                Debug.WriteLine("타이머 갱신");
            };
            _activeTimer.Start();
        }
    }
}
