namespace ExamSocket.Services
{
    /// <summary>
    /// 통신 서비스를 만들기 위한 인터페이스입니다.
    /// IDisposable을 상속받아, 사용 후 메모리 정리가 필요함을 명시합니다.
    /// </summary>
    public interface ICommunicationService : IDisposable
    {
        event Action<string>? MessageReceived; // 메시지 수신시 실행
        event Action<string>? ErrorOccurred; // 통신 중 에러 발생시 실행
        event Action? ConnectionEstablished; // 성공적으로 연결되었을 경우 실행

        /// <summary>
        /// 비동기로 진행되는 메서드
        /// 인수로 받은 포트를 열고 기다리는 역할
        /// </summary>
        /// <param name="port">연결할 포트번호</param>
        /// <returns></returns>
        Task StartAsync(int port);

        /// <summary>
        /// 내가 클라이언트가 되어 특정 ip, port로 연결을 시도한다. 
        /// 성공하면 true, 실패하면 false를 반환한다.
        /// </summary>
        /// <param name="ip">연결할 ip</param>
        /// <param name="port">연결할 port</param>
        /// <returns></returns>
        Task<bool> ConnectAsync(string ip, int port);

        /// <summary>
        /// 연결된 상태방에게 메시지를 보낸다.
        /// 전송 여부는 true/false로 반환한다.
        /// </summary>
        /// <param name="message">보낼 메시지</param>
        /// <param name="ip">전송할 ip</param>
        /// <param name="port">전송할 port</param>
        /// <returns></returns>
        Task<bool> SendMessageAsync(string message, string ip, int port);

        void Stop(); // 현재 진행 중인 모든 통신 서비스(대기, 연결 등)을 즉시 중단한다.
    }
}
