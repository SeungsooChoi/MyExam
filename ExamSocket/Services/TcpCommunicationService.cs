using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ExamSocket.Services
{
    /// <summary>
    /// 서버/클라이언트 모드를 모두 지원하는 양방향 통신 구현
    /// </summary>
    class TcpCommunicationService : ICommunicationService
    {
        private TcpListener? _listener; // 서버 모드 연결 대기용 리스너
        private TcpClient? _client; // 클라이언트 객체(송수신용)
        private NetworkStream _stream; // 데이터 송수신 스트림

        // 제어 및 상태
        private CancellationTokenSource? _cts; // 비동기 작업 취소 토큰
        private bool _isConnected; // 연결 상태

        public event Action<string>? MessageReceived;
        public event Action<string>? ErrorOccurred;
        public event Action? ConnectionEstablished;

        /// <summary>
        /// 서버 모드로 시작 > 지정된 포트에서 클라이언트 연결 대기
        /// </summary>
        /// <param name="port">리스닝할 포트 번호</param>
        /// <returns></returns>
        public async Task StartAsync(int port)
        {
            try
            {
                _cts = new CancellationTokenSource();

                // TCP 리스너 생성 및 시작 
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();

                // 시작 알림
                ErrorOccurred?.Invoke($"TCP 서버 시작됨 (포트:{port})");

                // 백그라운드에서 클라이언트 연결 대기 시작
                // UI 스레드를 블록하지 않기 위해 Task.Run 사용
                _ = Task.Run(async () => await AcceptClientAsync(_cts.Token), _cts.Token);
            }
            catch(SocketException ex)
            {
                // 포트가 이미 사용 중이거나 권한 부족 시 발생
                // 권한..?
                ErrorOccurred?.Invoke($"TCP 서버 시작 실패: {ex.Message}");
                throw; // 예외 전파
            }
            
        }
        /// <summary>
        /// 클라이언트 연결 대기 루프
        /// </summary>
        private async Task AcceptClientAsync(CancellationToken token)
        {
            try
            {
                // Stop() 호출시까지 연결 대기
                while (!token.IsCancellationRequested)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(token);

                    // 연결된 클라이언트가 있다면 거부. (1:1 통신만 지원한다)
                    if (_isConnected)
                    {
                        client.Close();
                        continue; // 다음 연결 대기
                    }

                    // 연결 수락 및 스트림 설정
                    _client = client;
                    _stream = _client.GetStream(); // 데이터 송수신용
                    _isConnected = true;

                    // 연결된 클라이언트 정보 출력
                    if (_client.Client.RemoteEndPoint is IPEndPoint remoteEndPoint)
                    {
                        ErrorOccurred?.Invoke($"클라이언트 연결됨: {remoteEndPoint.Address}:{remoteEndPoint.Port}");
                    }
                    else
                    {
                        ErrorOccurred?.Invoke("클라이언트 연결됨: 알 수 없는 엔드포인트");
                    }
                    ConnectionEstablished?.Invoke();  // UI에 연결 알림

                    _ = Task.Run(async () => await ReceiveMessagesAsync(token), token); // 수신 Task 시작 
                }
            }
            catch (OperationCanceledException)
            {
                // Stop() 호출로 인한 정상 종료 - 예외 무시
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"연결 대기 중 오류: {ex.Message}");
            }
        }
        /// <summary>
        /// 클라이언트 모드로 원격 서버에 연결
        /// </summary>
        /// <param name="ip">서버 IP 주소</param>
        /// <param name="port">서버 포트</param>
        /// <returns>연결 성공 여부</returns>
        public async Task<bool> ConnectAsync(string ip, int port)
        {
            try
            {
                // 1. TCP 클라이언트 생성 및 서버 연결
                _client = new TcpClient();
                await _client.ConnectAsync(ip, port);

                // 2. 데이터 송수신 스트림 획득
                _stream = _client.GetStream();
                _isConnected = true;

                // 3. 연결 성공 알림
                ErrorOccurred?.Invoke($"서버에 연결됨: {ip}:{port}");
                ConnectionEstablished?.Invoke();

                // 4. 취소 토큰이 없으면 생성 (서버 모드에서 시작 안 한 경우)
                _cts ??= new CancellationTokenSource();

                // 5. 백그라운드에서 메시지 수신 시작
                _ = Task.Run(async () => await ReceiveMessagesAsync(_cts.Token), _cts.Token);

                return true;
            }
            catch (SocketException ex)
            {
                // 연결 실패: 서버가 없거나, 방화벽, 잘못된 IP/포트 등
                ErrorOccurred?.Invoke($"연결 실패: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// 메시지 수신 루프 (백그라운드 스레드)
        /// 서버/클라이언트 모두 동일한 로직 사용
        /// </summary>
        private async Task ReceiveMessagesAsync(CancellationToken token)
        {
            // 수신 버퍼: 한 번에 최대 4096바이트 읽기
            var buffer = new byte[4096];

            try
            {
                // 연결이 유지되는 동안 계속 수신
                while (!token.IsCancellationRequested && _stream != null)
                {
                    Debug.WriteLine("수신 대기 중...");
                    // 1. 스트림에서 데이터 읽기 (비동기)
                    //    데이터가 올 때까지 대기 (블로킹)
                    var bytesRead = await _stream.ReadAsync(buffer, token);

                    // 2. 읽은 바이트가 0 = 상대방이 연결 종료
                    if (bytesRead == 0)
                    {
                        ErrorOccurred?.Invoke("연결이 종료되었습니다.");
                        _isConnected = false;
                        break;  // 수신 루프 종료
                    }

                    // 3. 바이트 배열을 문자열로 변환 (UTF-8 인코딩)
                    var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // 4. 이벤트 발생 → UI에서 메시지 표시
                    MessageReceived?.Invoke(message);
                }
            }
            catch (OperationCanceledException)
            {
                // Stop() 호출로 인한 정상 종료
            }
            catch (Exception ex)
            {
                // 네트워크 오류, 스트림 닫힘 등
                if (!token.IsCancellationRequested)
                {
                    ErrorOccurred?.Invoke($"메시지 수신 오류: {ex.Message}");
                    _isConnected = false;
                }
            }
        }

        /// <summary>
        /// 메시지 전송 (서버/클라이언트 공통)
        /// </summary>
        /// <param name="message">전송할 메시지</param>
        /// <param name="ip">사용 안 함 (이미 연결된 스트림 사용)</param>
        /// <param name="port">사용 안 함</param>
        /// <returns>전송 성공 여부</returns>
        public async Task<bool> SendMessageAsync(string message, string ip, int port)
        {
            try
            {
                // 1. 연결 상태 확인
                if (_stream == null || !_isConnected)
                {
                    ErrorOccurred?.Invoke("연결되지 않았습니다.");
                    return false;
                }

                // 2. 문자열을 바이트 배열로 변환 (UTF-8)
                var data = Encoding.UTF8.GetBytes(message);

                // 3. 스트림에 데이터 쓰기 (비동기)
                await _stream.WriteAsync(data);

                // 4. 버퍼 플러시 (즉시 전송 보장)
                await _stream.FlushAsync();

                return true;
            }
            catch (Exception ex)
            {
                // 전송 실패: 연결 끊김, 네트워크 오류 등
                ErrorOccurred?.Invoke($"메시지 전송 실패: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// 모든 연결 종료 및 리소스 해제
        /// </summary>
        public void Stop()
        {
            // 1. 연결 상태 플래그 해제
            _isConnected = false;

            // 2. 모든 비동기 작업 취소
            _cts?.Cancel();

            // 3. 네트워크 리소스 해제 (순서 중요)
            _stream?.Close();      // 스트림 먼저 닫기
            _client?.Close();      // 클라이언트 닫기
            _listener?.Stop();     // 리스너 중지 (서버 모드)

            // 4. 참조 제거 (가비지 컬렉션 대상)
            _stream = null;
            _client = null;
            _listener = null;

            // 5. 취소 토큰 해제
            _cts?.Dispose();
            _cts = null;
        }

        public void Dispose() 
        {
            Stop();
        }
    }
}
