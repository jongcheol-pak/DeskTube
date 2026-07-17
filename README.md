# DeskTube

유튜브 동영상을 **바탕화면 배경**으로 재생하는 Windows 11 앱 (WinUI 3, MSIX 패키지형).

바탕화면 아이콘 **뒤**(WorkerW)에 배경창을 배치해, 아이콘·작업 표시줄을 가리지 않고 유튜브 영상이 배경화면처럼 재생됩니다.

## 핵심 기능

- **배경 재생**: 유튜브 URL(watch / youtu.be / shorts / embed / live)을 입력하면 바탕화면 배경에서 재생 (FR-1)
- **다중 모니터**: 재생할 모니터 다중 선택, 전체 모니터 동기 재생(마스터-미러 시각 보정), 소리는 지정한 1개 모니터에서만 (FR-3·4)
- **플레이리스트**: 최대 100개 리스트 × 1000개 항목 — 생성·이름변경·삭제, 항목 추가·삭제·드래그/우클릭 메뉴 정렬, 재생 중 변경 즉시 반영 (FR-6)
- **차트형 항목 목록**: 항목마다 순위·썸네일·제목·채널명을 표시(유튜브 oEmbed 자동 조회·로컬 캐시, 실패 시 URL 표시), 셔플듣기·전체듣기 버튼과 행별 "이 곡부터 듣기" 재생 지원 (FR-18)
- **반복 재생**: 목록이 끝나도 멈추지 않고 처음부터 계속 반복 — 재생 순서는 플레이리스트 화면에서 선택(셔플듣기 = 셔플 반복, 전체듣기·행 재생 = 순서대로 반복) (FR-7)
- **트레이 상주**: 창을 닫아도 트레이에서 재생/정지/볼륨/설정/종료 제어, 더블클릭으로 설정 열기 (FR-9)
- **부팅 자동 시작**: Windows 로그인 시 창 없이 트레이로 시작해 마지막 재생 설정으로 자동 재생 — 마지막으로 재생하던 항목부터 이어서 (FR-8)
- **앱 시작 후 자동 재생**: 설정 토글(기본 꺼짐) — 켜면 앱을 열 때 마지막으로 재생하던 항목부터 배경 재생을 자동 시작 (FR-19)
- **유튜브 로그인**: 본인 구글 계정으로 로그인하면 프리미엄 혜택(광고 없음)이 배경 재생에도 적용 — 세션 쿠키는 로컬 WebView2 프로필에만 저장 (FR-15)
- **자동 일시정지**: 전체 화면 앱 사용 중·배터리 절약 모드·화면 잠금 시 재생을 멈춰 자원 절약 (NFR-1, 토글 3종)
- **화질 스케일**: 렌더링 세로 해상도 제한(원본/1080/720/480)으로 시스템 부하 조절 (FR-13)
- **음소거 토글**: 설정의 음소거 카드에서 켬/끔 — 트레이 볼륨 체크와 상태 공유 (FR-5)
- **동영상 크기 모드**: 채움(16:9로 화면 덮고 크롭, 기본) / 맞춤(영상비 유지 레터박스) / 늘리기(화면에 꽉 차게 왜곡) (FR-16)
- **다크 전용 디자인**: 코럴 포인트(#F25C54)의 다크 UI 고정 — Claude Design 시안(DeskTube 1a) 기반, 색·치수는 `Resources/DesignTokens.xaml` 토큰으로 관리 (테마 변경 기능 없음 — FR-17 폐기)
- **부하 절감 옵션**: 소리 없는(미러) 모니터 화질을 720 이하로 하향(opt-in) + 정지·창 숨김 시 메모리 자동 반환 (NFR-2)
- **창 상태 복원**: 설정 창 크기·위치를 재시작 후 복원(WinUIEx), 커스텀 타이틀바(44px, 시스템 캡션 버튼)
- **다국어**: 한국어/English + 시스템 언어 추종 (NFR-4)
- **정보 화면**: 버전·개인정보 처리방침 요지·오픈소스 라이선스 목록(항목 클릭 시 해당 라이브러리 공식 사이트로 이동, 전문 파일은 패키지 동봉) (FR-11·12)

## 실행 방법

### 개발 실행
Visual Studio 2026에서 `DeskTube.slnx`를 열고 x64 + `DeskTube (Package)` 프로필로 F5 (패키지형이라 Package 프로필 필수).

### 빌드·테스트 (CLI)
```
dotnet build DeskTube.slnx -c Debug -p:Platform=x64
dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -c Debug -p:Platform=x64
dotnet format DeskTube.slnx --verify-no-changes
```

### MSIX 패키징 (사이드로드)
```
MSBuild.exe src/DeskTube/DeskTube.csproj -restore -p:Configuration=Release -p:Platform=x64 ^
  -p:GenerateAppxPackageOnBuild=true -p:UapAppxPackageBuildMode=SideloadOnly -p:AppxBundle=Never
```
산출물: `AppPackages/DeskTube_<버전>_x64_Test/DeskTube_<버전>_x64.msix` (개발용 미서명 — Store 제출 시 Store가 서명)

## 아키텍처

계층형(서비스 중심) + MVVM(CommunityToolkit.Mvvm). 도메인 레이어 분리 없음 — 규칙이 얇은 앱에 과한 추상화를 피함.

```
src/DeskTube/
├── App.xaml.cs            # 진입점 — 언어 선적용, 조용 시작 분기, 컴포지션 루트(AppServices) 초기화
├── MainWindow.xaml        # NavigationView 셸 (홈/플레이리스트/정보 + 설정)
├── Views/                 # HomePage(URL 즉시 재생), PlaylistsPage, SettingsPage, AboutPage,
│                          # LoginWindow(유튜브 로그인)
├── ViewModels/            # 페이지별 VM — App.Services(수동 컴포지션 루트) 소비
├── Services/
│   ├── PlaybackCoordinator# 재생 상태 단일 소유자 — 큐 진행·다중 모니터 동기·오디오 라우팅·크기 모드·미러 하향
│   ├── WallpaperHost      # WorkerW 부착/해제 (24H2 자식·구형 형제 이중 탐색), 배경 복구
│   ├── PlayerHost         # CoreWebView2Controller + player.html IFrame 브리지 (가상 호스트 서빙)
│   ├── PlaylistLibrary    # 리스트 CRUD + 상한 (100×1000)
│   ├── MonitorService     # 모니터 열거·변경 감지(WM_DISPLAYCHANGE)·대상 해석
│   ├── PowerPolicyService # 자동 일시정지 신호 합성 (전체화면 QUNS·배터리 세이버·세션 잠금)
│   ├── TrayIconService    # H.NotifyIcon 트레이 (메뉴 5항목)
│   ├── StartupService     # StartupTask 상태·조용 시작 판별 (-startup 인자 폴백)
│   ├── YouTubeSessionService # 로그인 세션 (SAPISID 쿠키) 확인·로그아웃
│   └── JsonStateStore     # settings/playlists JSON 원자적 영속화 (손상 .bak 복구)
├── Interop/               # WallpaperSurface(Win32 배경창)·WorkerW·모니터·세션·워킹셋 P/Invoke
└── Strings/               # en-US(폴백)·ko-KR .resw
```

### 재생 플로우
1. URL 입력(홈) 또는 플레이리스트 재생 → `PlaybackCoordinator.StartAsync`
2. 선택 모니터마다 `WallpaperHost.Attach`(WorkerW 뒤 배치) + `PlayerHost` 생성(WebView2)
3. `player.html`의 유튜브 IFrame API가 재생 — 오디오 대상 1곳만 소리, 나머지 음소거+시각 동기
4. 정지 시 전부 해제하고 원래 배경화면 복구

### 데이터
- 설정·플레이리스트: `ApplicationData.Current.LocalFolder`의 `settings.json` / `playlists.json` (원자적 쓰기, 손상 시 .bak 보존 후 기본값)
- 로그인 쿠키: 동일 폴더의 WebView2 프로필 (앱은 계정 정보를 저장하지 않음 — [개인정보 처리방침](docs/privacy-policy.md))

## Store 제출 (사용자 수행)

1. 현재 identity는 임시(`DeskTube.Dev`) — 파트너 센터에서 발급받은 identity로 `Package.appxmanifest` 교체 후 재패키징
2. WACK 통과 확인 (`docs/verification-2026-07.md` 절차)
3. 개인정보 처리방침 호스팅 URL 확보 후 정보 화면 문구(`PrivacyDocNote`)를 URL로 교체

## 문서

- 요구사항: `docs/prd.md`
- 검증 기록: `docs/verification-2026-07.md`
- 계획: `docs/plans/`
- 에이전트 가이드: `AGENTS.md`
