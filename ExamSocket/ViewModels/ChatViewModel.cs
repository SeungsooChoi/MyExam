using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExamSocket.Models;
using ExamSocket.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace ExamSocket.ViewModels
{
    public partial class ChatViewModel : ObservableObject
    {
        private ICommunicationService? _communicationService;
        private readonly MessageRepository _messageRepository;

        private DispatcherTimer _idleTimer; // 연결이 성공(ConnectAsync / OnConnectionEstablished)하면 타이머를 시작
        private const int IdleTimeoutSeconds = 300; // 300초 (5분)

        [ObservableProperty]
        private long myUserNo;
        [ObservableProperty]
        private long targetUserNo;

        public ChatViewModel(long loggedInUserId)
        {
            myUserNo = loggedInUserId;

            _idleTimer = new DispatcherTimer();
            _idleTimer.Interval = TimeSpan.FromSeconds(IdleTimeoutSeconds);
            _idleTimer.Tick += OnIdleTimeout;

            _messageRepository = new MessageRepository();
        }

        /// <summary>
        /// 시간 초과될경우 실행
        /// </summary>
        private async void OnIdleTimeout(object? sender, EventArgs e)
        {
            Debug.WriteLine($"OnIdleTimeout :: START IsConnected : ${IsConnected}");
            _idleTimer.Stop();

            if (IsConnected)
            {
                // 1. 상대방에게 로그아웃 사실 알림
                int remotePort = Convert.ToInt32(RemotePort);
                await _communicationService!.SendMessageAsync("[자동 알림] 상대방이 장시간 활동이 없어 연결이 종료되었습니다.", RemoteIp, remotePort);

                // 2. 시스템 메시지 표시
                AddSystemMessage($"{IdleTimeoutSeconds}초 동안 활동이 없어 자동 로그아웃 되었습니다.");

                // 3. 연결 종료
                await DisConnect();
            }
        }

        // 타이머 리셋 메서드 (활동 감지 시 호출)
        private void ResetIdleTimer()
        {
            Debug.WriteLine($"ResetIdleTimer :: START IsConnected : ${IsConnected}");

            if (IsConnected)
            {
                _idleTimer.Stop();
                _idleTimer.Start();
            }
        }

        /// <summary>
        /// 컬렉션의 변경 사항을 UI에 반영하도록 ObservableCollection 사용
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ChatMessage> _messages = new ObservableCollection<ChatMessage>();

        [ObservableProperty] 
        private string _messageInput = string.Empty; // 메시지 입력값

        [ObservableProperty] private string _myPort = "5000"; // 기본 포트 설정
        [ObservableProperty] private string _remoteIp = "127.0.0.1"; // 기본 ip 설정
        [ObservableProperty] private string _remotePort = "5001"; // 상대방 기본 포트 설정
        [ObservableProperty] private bool _isTcpProtocol = true;
        [ObservableProperty] private bool _isUdpProtocol = false;
        [ObservableProperty] private string _connectionButtonText = "연결";
        [ObservableProperty] private bool _isConnected = false;

        public event Action? ScrollRequested; // 스크롤을 끝까지 내려달라고 하는 신호

        [RelayCommand]
        private async Task ToggleConnection()
        {
            if (IsConnected)
            {
                await DisConnect();
            }
            else
            {
                await ConnectAsync();
            }
        }
        [RelayCommand]
        private async Task SendMessage()
        {
            // 입력 유효성 검사 (빈 메시지 무시)
            if (string.IsNullOrWhiteSpace(MessageInput)) return;
            
            // 연결 상태 확인
            if (!IsConnected)
            {
                AddSystemMessage("연결되지 않았습니다.");
                return;
            }

            ResetIdleTimer(); // 타이머 리셋

            // 메시지 처리
            string message = MessageInput.Trim();
            MessageInput = string.Empty;

            //  패킷 조립: "내숫자ID|메시지내용"
            string packet = $"{MyUserNo}|{message}";

            // 포트 번호
            int remotePort = Convert.ToInt32(RemotePort);
            if(remotePort <= 0)
            {
                AddSystemMessage("알맞은 포트번호를 입력하세요.");
                return;
            }

            // 5. 메시지 전송: 비동기로 원격 서버에 전송 시도
            var success = await _communicationService!.SendMessageAsync(packet, RemoteIp, remotePort);

            // DB 저장 (UI가 멈추지않기위해 비동기로진행)
            if (success)
            {
                await _messageRepository.SaveMessageAsync(MyUserNo, TargetUserNo, message);
            }

            // 6. 채팅 메시지 객체 생성: UI에 표시할 메시지 데이터 구성
            var chatMessage = new ChatMessage
            {
                Content = message,              // 메시지 내용
                Timestamp = DateTime.Now,       // 전송 시각
                IsSent = true,                  // 보낸 메시지 표시
                Status = success ? MessageStatus.Sent : MessageStatus.Failed  // 전송 결과
            };

            // 7. UI 스레드에서 메시지 추가: WPF UI 업데이트는 반드시 UI 스레드에서 실행
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(chatMessage);      // ObservableCollection에 메시지 추가
                ScrollRequested?.Invoke();      // 스크롤을 최하단으로 이동 (새 메시지 보이기)
            });
        }

        private async Task LoadChatHistoryAsync()
        {
            if(MyUserNo <= 0 || TargetUserNo <= 0) return; // id를 알아야함.

            List<ChatMessage> history = await _messageRepository.GetRecentMessageAsync(MyUserNo, TargetUserNo);

            // UI 업데이트
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Clear(); // 기존 화면 비우기(중복방지)

                foreach (var message in history)
                {
                    Messages.Add(message);
                }

                // 시스템 메시지 추가
                Messages.Add(new ChatMessage
                {
                    Content = "---------- 여기부터 최근 대화입니다 ----------",
                    IsSent = false,
                    Status = MessageStatus.Received,
                    Timestamp = DateTime.Now
                });

                ScrollRequested?.Invoke(); // 스크롤 맨 아래로
            });
        }

        /// <summary>
        /// 연결 해제
        /// </summary>
        private async Task DisConnect()
        {
            _idleTimer.Stop();

            _communicationService?.Stop();
            IsConnected = false;
            ConnectionButtonText = "연결";
            AddSystemMessage("연결이 종료되었습니다.");
        }

        /// <summary>
        /// 시스템 알림을 채팅창에 표시
        /// </summary>
        /// <param name="message"></param>
        private void AddSystemMessage(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new ChatMessage
                {
                    Content = $"[시스템] {message}",
                    Timestamp = DateTime.Now,
                    IsSent = false,
                    Status = MessageStatus.Received
                });
                ScrollRequested?.Invoke(); // 스크롤 이동
            });
        }

        /// <summary>
        /// 연결 시도
        /// </summary>
        /// <returns></returns>
        private async Task ConnectAsync()
        {
            try
            {
                int myPort = Convert.ToInt32(MyPort);
                int remotePort = Convert.ToInt32(RemotePort);

                // 포트 유효성 검사
                if (myPort <= 0 || myPort > 65535)
                {
                    AddSystemMessage("올바른 포트 번호를 입력하세요 (1-65535)");
                    return;
                }

                if (remotePort <= 0 || remotePort > 65535)
                {
                    AddSystemMessage("올바른 상대방 포트 번호를 입력하세요 (1-65535)");
                    return;
                }

                // 서비스 생성
                _communicationService?.Dispose();
                _communicationService = IsTcpProtocol
                    ? new TcpCommunicationService()
                    : new UdpCommunicationService();

                // 이벤트 구독
                _communicationService.MessageReceived += OnMessageReceived;
                _communicationService.ErrorOccurred += OnErrorOccurred;
                _communicationService.ConnectionEstablished += OnConnectionEstablished;

                // 서버 시작
                await _communicationService.StartAsync(myPort);

                // 상대방에게 연결 시도
                var connectResult = await _communicationService.ConnectAsync(RemoteIp, remotePort);

                if (!connectResult && IsTcpProtocol)
                {
                    AddSystemMessage("TCP 연결 실패. 상대방의 연결을 기다립니다...");
                }
                else
                {
                    IsConnected = true;
                    ConnectionButtonText = "연결 종료";
                    AddSystemMessage($"{(IsTcpProtocol ? "TCP" : "UDP")} 연결됨");

                    // 연결 성공, ID에 대한 핸드쉐이크 패킷 전송
                    string handshakePacket = $"{MyUserNo}|[HANDSHAKE]";
                    await _communicationService.SendMessageAsync(handshakePacket, RemoteIp, remotePort);

                    // 메시지 입력 타이머 시작
                    ResetIdleTimer();
                }
            }
            catch (Exception ex)
            {
                AddSystemMessage($"연결 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 상대방으로부터 메시지가 도착했을 때 화면에 표시
        /// </summary>
        private async void OnMessageReceived(string packet)
        {
            Debug.WriteLine(packet);

            long senderNo = 0;
            string content = packet; // 기본값은 전체
            bool isFirstContact = TargetUserNo == 0;

            // 패킷 분해 (구분자 | 기준)
            var parts = packet.Split('|', 2);

            if (parts.Length == 2)
            {
                long.TryParse(parts[0], out senderNo);
                content = parts[1];
            }

            // 핸드쉐이크 패킷인지 확인
            if(content == "[HANDSHAKE]")
            {
                // 처음 식별된 상대방이거나, ID가 변경된 경우
                if (TargetUserNo == 0 || TargetUserNo != senderNo)
                {
                    TargetUserNo = senderNo;

                    Debug.WriteLine($"상대방 식별됨: {TargetUserNo}");

                    // 대화 내역 로드
                    await LoadChatHistoryAsync();

                    // 상대방에게 ID 핸드쉐이크
                    string myHandshake = $"{MyUserNo}|[HANDSHAKE]";
                    int remotePort = Convert.ToInt32(RemotePort);
                    await _communicationService!.SendMessageAsync(myHandshake, RemoteIp, remotePort);
                }

                // 채팅 처리 안함
                return;
            }

            // ---------------------------- 일반 채팅 메시지 처리
            // 혹시라도 일반 메시지가 먼저 왔을 경우를 대비해 ID 업데이트
            if (TargetUserNo == 0 || TargetUserNo != senderNo)
            {
                TargetUserNo = senderNo;
            }

            ChatMessage chatMessage = new ChatMessage
            {
                Content = content,
                Timestamp = DateTime.Now,
                IsSent = false,
                Status = MessageStatus.Received
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(chatMessage);
                ScrollRequested?.Invoke();
            });
        }

        private void OnErrorOccurred(string error)
        {
            AddSystemMessage(error);
        }

        private void OnConnectionEstablished()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsConnected = true;
                ConnectionButtonText = "연결 종료";
            });
        }
        public void Dispose()
        {
            _idleTimer.Stop();
            _communicationService?.Dispose();
        }
    }
}
