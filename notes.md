# DeskTube 작업 내역

## 최근 변경
- 2026-07-16: **part1 코어 엔진 구현 완료 (T1~T8)** — plan: `docs/plans/2026-07-15-desktube-core-part1.md`, PRD: `docs/prd.md`
  - **무엇을**: 유튜브 동영상 배경화면 앱의 코어 엔진 8개 task. ① WinUI 3 패키지형 솔루션 스캐폴딩(WinAppSDK 2.2, slnx 플랫폼 매핑, xUnit x64 고정, git init) ② Models+JSON 영속화(JsonStateStore 원자적 쓰기·손상 .bak 복구, PlaylistLibrary 상한 100×1000, Result/ErrorCode 패턴, AppLog) ③ 유튜브 URL 파서(watch/youtu.be/shorts/embed/live)+재생 큐(순차/셔플/랜덤/한곡·전체반복, 셔플 사이클 소진 후 재셔플) ④ 모니터 서비스(EnumDisplayMonitors, WM_DISPLAYCHANGE 메시지 창, 선택/오디오 대상 폴백) ⑤ WorkerW 배경창(24H2 자식/구형 형제 이중 탐색, SetParent 부착·해제, SPI 배경 복구, EnsureHealthy 재부착) ⑥ WebView2 플레이어(player.html IFrame 브리지, 가상 호스트 `player.desktube.local`, autoplay 워치독, 화질 스케일 CSS transform, 오류 로그) ⑦ 재생 오케스트레이터(마스터-미러 동기 5틱/1초 보정, 오디오 1개 모니터만, Ended 중복 억제, 모니터 변경 재진입 가드, FireAndForget 예외 포착) ⑧ 자동 일시정지(전체화면 QUNS 2초 폴링·배터리 세이버·세션 잠금 3신호 합성, 모두 해제 시에만 재개)
  - **왜**: PRD FR-1~7·13·14, NFR-1 충족 (Store 배포용 배경화면 앱의 UI 이전 단계)
  - **어떻게**: 계층형(서비스 중심) + 수동 컴포지션 루트(AppServices — DI 컨테이너는 미승인 의존성이라 보류, plan D13), 재생 상태 단일 소유자 PlaybackCoordinator, interop은 IWallpaperHost/IPlayerHost seam으로 분리해 목킹 테스트
  - **검증 결과**: `dotnet build --no-incremental -p:Platform=x64` 경고 0 / `dotnet test -p:Platform=x64` 75/75 통과 / `dotnet format` 0건. 매 task spec+quality 이중 리뷰(총 BLOCKER 2·MAJOR 8 발견·전부 수정 후 재검증 OK). HUMAN-VERIFY 잔여: 아이콘 뒤 렌더링·종료 복구(T5), 소리 자동재생·화질 스케일·임베드 금지 로그(T6), 2모니터 동기(T7), 전체화면/잠금/세이버 실기(T8), 클린 설치 최초 실행(T2)
  - **변경 파일**: `DeskTube.slnx`, `src/DeskTube/`(App·MainWindow·csproj·manifest·Assets/player.html·Models 4·Services 12·Interop 3·Views/WallpaperWindow), `tests/DeskTube.Tests/`(테스트 7파일, 75케이스)
  - **주요 함정 (재발 방지)**: ① 존재하지 않는 타입 참조 시 XAML 컴파일러가 CS 오류 대신 WMC9999(내부 NRE)로 죽음 — WebView2 예외는 HResult 0x80070002로 판별 ② 경고 확인은 `--no-incremental` 전체 재빌드 필수(증분 빌드가 미변경 프로젝트 경고 미출력) ③ 테스트 호스트에서 WinAppSDK 모듈 자동 초기화가 0x80040154로 죽음 — `WindowsAppSdkAutoInitialize=false` + App 생성자 명시 초기화 ④ `dotnet test`에 `-p:Platform=x64` 필수
  - **미처리 Deferred**: part2 실행(`docs/plans/2026-07-15-desktube-ui-part2.md` — 트레이·설정 UI·플레이리스트 UI·자동 시작·로그인·정보·다국어·WACK), AGENTS.md Test 명령 플래그 갱신(사용자 승인 필요), DI 컨테이너 도입 여부(part2 재검토·사용자 확인 필요), Store 제출(사용자 수행)

## 아카이브 인덱스
