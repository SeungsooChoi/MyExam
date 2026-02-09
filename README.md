### 목적
- Windows 응용 소프트웨어는 Socket 통신을 진행하는 경우가 많음.
- 이 프로젝트를통해 Socket 통신의 기본을 충분하게 익히고, 더 나아가 C#에 대한 기본 문법을 익히는 것에 사용.

### 개요
- C# WPF (.NET 8.0)를 사용하여 실시간 1:1 채팅 프로그램을 구현한다.
- 동일 네트워크 혹은 동일 로컬 환경(Loopback, 1 PC)에서 두 개의 클라이언트(인스턴스)를 실행하여 상호 통신이 가능해야 한다.
- 통신 프로토콜은 TCP와 UDP 중 사용자가 UI에서 선택하여 연결할 수 있어야 한다.
- 채팅 기능에 Microsoft SQL Server(SSMS)를 연동하여 채팅 기록, 사용자 관리 기능 등을 추가한다.
- 단순 실시간 통신을 넘어, 사용자 인증(로그인)과 채팅 이력 보존(History) 기능을 추가하여 데이터 관리 능력을 기른다.
- 모든 데이터 접근 로직은 DB 연결을 통해 이루어져야 한다. (로그인, 채팅 이력 등등 모두 DB에 보관)
    - 로컬에서 파일 형태 혹은 휘발성 메모리 내에서 관리하지 않는다.
 
### 조건
- 언어: C# (필수)
- 프레임워크: .NET 8.0 권장 (최소 .NET 5.0 이상)
- 아키텍처: MVVM 패턴 준수 (View와 ViewModel의 의존성 분리, 부득이한 UI 제어 로직만 Code-behind 허용)
- 라이브러리: MVVM 패턴 구현을 위한 CommunityToolkit.Mvvm 등 서드파티 라이브러리 허용.
[중요] 소켓 통신은 반드시 MS 기본 라이브러리인 System.Net.Sockets를 사용해야 함. (통신 관련 외부 라이브러리 사용 불가)
- DB 버전 : DB 사용 버전은 무관하나, 최대한 최신 버전의 DB를 사용한다. (Ex. SSMS 2022)
- DB 환경 : Microsoft SQL Server (SSMS 사용, LocalHost로 구축)

### 요구사항
- 프로그램 기본 설정 :
    - 프로그램 실행 시, 자신의 포트(Bind Port)와 상대방의 접속 정보(IP, Port)를 각각 설정할 수 있는 UI 또는 설정 파일 로드 기능을 제공해야 한다.
    - 프로토콜 전환: UI에서 TCP와 UDP 모드를 선택/전환할 수 있어야 한다.
- 메시지 송수신:
    - 프로그램 2개를 실행하여 실시간으로 메시지를 주고받는다.
    - 채팅 창에는 [보낸 시간] 메시지 내용 [전송 상태] 형식이 포함되어야 한다. (UI 디자인은 자유)
- 예외 처리:
    - 네트워크 연결 끊김, 수신 대기 실패 등 SocketException 발생 시 프로그램이 종료되지 않고 사용자에게 알림을 주거나 적절히 처리해야 한다.
- 사용자 인증 및 관리:
	- 프로그램 시작 시 로그인을 진행해야 한다.
	- 로그인은 중복 로그인을 지원하지 않는다. (한 클라이언트에 접속이 되어 있는 경우, 다른 클라이언트에서 로그인할 수 없다.)
	- 사용자가 일정 시간(300초) 동안 채팅을 치지 않으면 해당 유저는 로그아웃되고, 상대방에게 로그아웃 되었음을 표시한다.
	- 회원가입 기능(또는 미리 DB에 저장된 ID/PW 대조)을 통해 인증된 사용자만 채팅창에 진입할 수 있다.
- 채팅 이력(History) 자동 로드:
	- 채팅방 입장 시, 해당 사용자와 상대방이 주고받았던 최근 대화 내역(최소 30개 이상)을 DB에서 불러와 화면에 표시해야 한다.
	- 날짜별/시간별로 정렬되어 출력되어야 한다. (데이터 손실은 없어야 한다.)
- 실시간 메시지 영구 저장:
	- 메시지 송/수신 발생 시, 소켓 전송과 동시에 DB의 Messages 테이블에 실시간으로 저장되어야 한다.
	- 저장 정보 : 보낸 사람, 메시지 내용, 전송 시간 (정확한 DateTime 값)가 기본이며, 필요한 값들에 대하여 추가 저장할 수 있다.
- 비동기 DB 작업 (UX 개선):
	- DB 연결 및 쿼리 실행 시 UI 스레드가 멈추지 않도록 비동기(async/await) 방식을 적용해야 한다.


### 폴더 구조
```root/
├── ExamSocket/
│   ├── Models/
│   │   ├── ChatMessage.cs: 채팅 메시지
│   │   └── MessageProtocol.cs: 신뢰성 있는 UDP 통신을 위한 ACK 확인용 클래스
│   ├── Services/
│   │   ├── AuthService.cs: IAuthService 인터페이스를 구현하는 회원가입 및 로그인을 위한 클래스
│   │   ├── IAuthService.cs
│   │   ├── ICommunicationService.cs: 통신 서비스를 만들기 위한 인터페이스.
│   │   ├── MessageRepository.cs: App.config에서 DB 연결 설정 ConnectionString을 조회하고 채팅 DB 저장 및 조회
│   │   ├── PasswordHasher.cs: 비밀번호를 받아서 salt와 함께 해싱, Verify기능이 구현된 클래스
│   │   ├── TcpCommunicationService.cs: ICommunicationService 인터페이스를 구현하는 TCP 통신 전담 클래스
│   │   └── UdpCommunicationService.cs: ICommunicationService 인터페이스를 구현하는 UDP 통신 전담 클래스
│   ├── ViewModels/
│   │   ├── ChatViewModel.cs: 채팅 ViewModel
│   │   ├── LoginViewModel.cs: 로그인 ViewModel
│   │   └── SignupViewModel.cs: 회원가입 ViewModel
│   ├── Views/
│   │   ├── LoginWindow.xaml: 로그인 View
│   │   ├── MainWindow.xaml: 채팅 View
│   │   └── SignupWindow.xaml: 회원가입 View
│   └── App.xaml
└── MyExam.slnx : 솔루션 파일
```


### 화면
- 로그인
<img width="346" height="419" alt="image" src="https://github.com/user-attachments/assets/e115ead1-1f7f-4ac8-b7ae-90259d08a2c8" />

- 회원가입
<img width="346" height="443" alt="image" src="https://github.com/user-attachments/assets/6e232d4c-909d-4340-8b6c-9048d561d390" />

- 로그인 시 메인 화면
<img width="786" height="443" alt="image" src="https://github.com/user-attachments/assets/19a59f23-a14f-41ba-b900-b730ee642166" />

- 연결 후
<img width="786" height="443" alt="image" src="https://github.com/user-attachments/assets/2c5ca7b1-95d9-4e02-a800-bd56ae92a859" />
<img width="786" height="443" alt="image" src="https://github.com/user-attachments/assets/c94cd8de-99e8-4578-8f47-d615bb20f9e3" />

- 상대방 연결 종료 시
<img width="786" height="443" alt="image" src="https://github.com/user-attachments/assets/0d4e17b8-6095-4dd1-a4bd-a89f390ba42f" />





