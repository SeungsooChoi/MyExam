namespace ExamSocket.Models
{
    public enum PacketType
    {
        Message, // 일반 대화
        Ack      // 수신 확인 신호
    }
    public class MessageProtocol
    {
        public PacketType Type { get; set; }
        public string Id { get; set; } = Guid.NewGuid().ToString(); // 패킷 고유 번호 (순서 체크용)
        public string Content { get; set; } = string.Empty; // 실제 내용
    }
}
 