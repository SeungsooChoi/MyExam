using Azure.Messaging;
using ExamSocket.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ExamSocket.Services
{
    /// <summary>
    /// ICommunicationService 인터페이스를 구현하는 UDP 통신 전담 클래스
    /// </summary>
    internal class UdpCommunicationService : ICommunicationService
    {
        // 통신 객체
        private UdpClient? _udpClient;
        // 비동기 작업 취소용 토큰
        private CancellationTokenSource? _cts;
        // 서비스 시작 확인용 플래그
        private bool _isStarted;

        // 보낸 메시지 ACK 기다리는 대기열
        private ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingAcks = new();

        // 메시지 오면 UI에 notice
        public event Action<string>? MessageReceived;
        // 에러 발생시 UI에 로그 표시
        public event Action<string>? ErrorOccurred;
        // 연결되면 UI 상태 변경 
        public event Action? ConnectionEstablished;

        /// <summary>
        /// UDP 소켓을 열고 수신 대기를 시작하는 메서드
        /// </summary>
        /// <param name="port">포트 번호</param>
        public async Task StartAsync(int port)
        {
            try
            {
                // UDP 클라이언트 생성 및 포트 바인딩
                _udpClient = new UdpClient(port);

                // 수신 중단용 토큰 생성
                _cts = new CancellationTokenSource();
                _isStarted = true;

                // 상태 알림(로그 및 UI 버튼 변경용)
                ErrorOccurred?.Invoke($"UDP 시작됨 (포트: {port})");
                ConnectionEstablished?.Invoke();
                
                // 백그라운드 수신 루프 시작.
                // 메인 UI 스레드와 별개로 수신 작업 돌림(Task)
                _ = Task.Run(async () => await ReceiveMessagesAsync(_cts.Token), _cts.Token);
            }
            catch (SocketException ex)
            {
                // 이미 포트가 사용 중이거나 권한이 없을 경우
                ErrorOccurred?.Invoke($"UDP 시작 실패: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 상대방 IP/Port 설정 메서드
        /// </summary>
        public Task<bool> ConnectAsync(string ip, int port)
        {
            // UDP는 연결이 필요 없으므로 즉시 성공 반환
            ErrorOccurred?.Invoke($"UDP 모드: 상대방 주소 설정됨 ({ip}:{port})");
            return Task.FromResult(true);
        }

        /// <summary>
        /// 메시지를 기다리고 받는 루한 루프 메서드
        /// </summary>
        private async Task ReceiveMessagesAsync(CancellationToken token)
        {
            // 토큰이 취소되지 않았고, 클라이언트가 살아있다면 루프
            while (!token.IsCancellationRequested && _udpClient != null)
            {
                try
                {
                    // 데이터 수신 대기
                    var result = await _udpClient.ReceiveAsync(token);

                    // 받은 바이트 배열을 문자열로 변환
                    var message = Encoding.UTF8.GetString(result.Buffer);

                    // 1. JSON 역직렬화 (문자열 -> 객체)
                    MessageProtocol? packet = null;

                    try
                    {
                        packet = JsonSerializer.Deserialize<MessageProtocol>(message);
                    }
                    catch
                    {
                        // JSON 형식이 아니면 무시
                        continue;
                    }

                    if (packet == null) continue;

                    // 2. 패킷 타입에 따른 분기 처리
                    if (packet.Type == PacketType.Ack)
                    {
                        // ACK 응답받음(상대방이 수신받았다는 응답)
                        // 대기열에서 해당 ID를 찾아 "완료됨(true)"으로 설정
                        if (_pendingAcks.TryRemove(packet.Content, out var tcs))
                        {
                            tcs.TrySetResult(true);
                        }
                    }
                    else if (packet.Type == PacketType.Message)
                    {
                        // 메시지 응답받음
                        // 1. 즉시 "잘 받았다"고 ACK 전송 (반송)
                        await SendAckAsync(packet.Id, result.RemoteEndPoint);

                        // 2. 화면에 표시 (이벤트 발생)
                        MessageReceived?.Invoke(packet.Content);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Stop() 메서드에서 _cts.Cancel() 호출시
                    // 정상 종료
                    Debug.WriteLine("정상 종료");
                }
                catch (SocketException ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        ErrorOccurred?.Invoke($"메시지 수신 오류: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        ErrorOccurred?.Invoke($"메시지 수신 오류: {ex.Message}");
                    }
                }
            }
        }

        private async Task SendAckAsync(string receivedPacketId, IPEndPoint iPEndPoint)
        {
            MessageProtocol ackPacket = new MessageProtocol
            {
                Type = PacketType.Ack,
                Content = receivedPacketId,
                // Id는 기본값이 설정되어있음.
            };

            string json = JsonSerializer.Serialize(ackPacket);
            byte[] data = Encoding.UTF8.GetBytes(json);

            // 전송
            await _udpClient!.SendAsync(data, iPEndPoint);
        }

        /// <summary>
        /// 메시지 전송 메서드
        /// </summary>
        public async Task<bool> SendMessageAsync(string message, string ip, int port)
        {
            if (_udpClient == null || !_isStarted) return false;

            // 1. 전송할 패킷 객체 생성
            MessageProtocol packet = new MessageProtocol
            {
                Type = PacketType.Message,
                Content = message 
            };

            // 2. 직렬화 (객체 -> JSON 문자열 -> 바이트)
            var json = JsonSerializer.Serialize(packet);
            var data = Encoding.UTF8.GetBytes(json);
            var endPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            // 3. ACK 대기용 신호기 생성 및 등록
            var tcs = new TaskCompletionSource<bool>();
            _pendingAcks[packet.Id] = tcs;

            // 4. 재전송 설정 (최대 3번 시도, 1초 대기)
            int retryCount = 0;
            const int MaxRetries = 3;
            const int TimeoutMillis = 1000;

            try
            {
                while (retryCount < MaxRetries)
                {
                    // 전송
                    await _udpClient.SendAsync(data, endPoint);

                    // 대기: (ACK가 오거나) OR (1초가 지나거나) 둘 중 하나가 먼저 될 때까지
                    var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeoutMillis));

                    if (completedTask == tcs.Task)
                    {
                        // ACK가 먼저 도착함 -> 성공!
                        return true;
                    }

                    // 시간 초과 -> 재전송 루프 계속
                    retryCount++;
                    Debug.WriteLine($"[UDP] 응답 없음. 재전송 {retryCount}/{MaxRetries}");
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"전송 중 오류: {ex.Message}");
            }
            finally
            {
                // 끝나면 대기열에서 제거 (메모리 누수 방지)
                _pendingAcks.TryRemove(packet.Id, out _);
            }

            // 여기까지 오면 3번 다 실패한 것임
            ErrorOccurred?.Invoke("전송 실패: 상대방의 응답이 없습니다.");
            return false;
        }

        /// <summary>
        /// 서비스 종료 및 리소스 해제
        /// </summary>
        public void Stop()
        {
            _isStarted = false;

            // 수신 루프를 멈추기 위해 취소 신호
            _cts?.Cancel();

            // 소켓 닫기
            _udpClient?.Close();
            _udpClient = null;

            _cts?.Dispose();
            _cts = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
