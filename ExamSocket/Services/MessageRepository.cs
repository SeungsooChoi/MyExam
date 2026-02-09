using ExamSocket.Models;
using Microsoft.Data.SqlClient;
using System.Configuration;
using System.Diagnostics;

namespace ExamSocket.Services
{
    public class MessageRepository
    {
        private readonly string _connectionString;
        public MessageRepository()
        {
            // App.config에서 DB 연결 설정 가져옴
            _connectionString = ConfigurationManager.ConnectionStrings["MyDbConnection"].ConnectionString;
        }
        public async Task SaveMessageAsync(long senderNo, long targetNo, string content)
        {
            string query = @"INSERT INTO message_histories (user_id, target_id, content, created_at)
                VALUES (@senderId, @targetId, @content, GETDATE())";

            Debug.WriteLine($"senderId: {senderNo}");
            Debug.WriteLine($"targetId: {targetNo}");
            Debug.WriteLine($"content: {content}");

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync(); // 비동기 연결

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@senderId", senderNo);
                        cmd.Parameters.AddWithValue("@targetId", targetNo);
                        cmd.Parameters.AddWithValue("@content", content);

                        await cmd.ExecuteNonQueryAsync(); // 비동기 실행
                    }
                }
                Debug.WriteLine("[DB 저장 완료]");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB 저장 실패] {ex.Message}"); // 채팅은 유지되어야함. 로그만 남김
            }
        }

        public async Task<List<ChatMessage>> GetRecentMessageAsync(long myId, long targetId, int count = 30)
        {
            List<ChatMessage> list = new List<ChatMessage>();

            // 1. 쿼리 작성 (나<->상대방 사이의 대화만, 최신순 30개)
            string query = @"SELECT TOP (@count) user_id, content, created_at
                            FROM message_histories
                            WHERE (user_id = @myId AND target_id = @targetId) 
                               OR (user_id = @targetId AND target_id = @myId)
                            ORDER BY created_at DESC";

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@myId", myId);
                        cmd.Parameters.AddWithValue("@targetId", targetId);
                        cmd.Parameters.AddWithValue("@count", count);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                long senderId = Convert.ToInt64(reader["user_id"]);
                                string content = reader["content"].ToString();
                                DateTime time = Convert.ToDateTime(reader["created_at"]);

                                // 내가 보낸건지 확인
                                bool isSentByMe = (senderId == myId);

                                list.Add(new ChatMessage
                                {
                                    Content = content,
                                    Timestamp = time,
                                    IsSent = isSentByMe,
                                    Status = MessageStatus.Sent // 과거 내역은 전송 완료로 침
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[대화 불러오기 실패] {ex.Message}");
            }

            // DB에서는 최신순(DESC)으로 가져왔으니, 화면에는 과거->최신 순으로 보여주기 위해 뒤집음
            list.Reverse();
            return list;
        }
    }
}
