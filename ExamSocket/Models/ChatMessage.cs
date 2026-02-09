using System.Windows;

public enum MessageStatus
{
    Sent, // 전송 성공
    Failed, // 전송 실패
    Received // 수신
}

namespace ExamSocket.Models
{
    /// <summary>
    /// 채팅 메시지의 정보를 담음
    /// </summary>
    public class ChatMessage
    {
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsSent { get; set; } // 보낸 메시지면 true
        public MessageStatus Status { get; set; } // 메시지의 현재 상태

        // 화면 표시 영역
        public string DisplayText => IsSent
         ? $"나: {Content} [{Status}]"
         : $"상대방: {Content}";

        // 메시지 말풍선의 배경색 (내가 보낸 건 연한 파랑, 받은 건 연한 회색)
        public string BackgroundColor => IsSent ? "#E3F2FD" : "#F5F5F5";

        // 글자색 (진한 회색/검정 계열)
        public string ForegroundColor => "#212121";

        // 메시지 정렬 위치 (내가 보낸 건 오른쪽, 받은 건 왼쪽에 배치)
        public HorizontalAlignment Alignment => IsSent
             ? HorizontalAlignment.Right
             : HorizontalAlignment.Left;
    }
}
