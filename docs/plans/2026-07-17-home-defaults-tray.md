# Plan: 기본값 변경 + 홈 URL 영속·자동 재생 예외 + 홈 로그인 상태 + 재생 실패 스킵 보강 + 트레이 메뉴 재작성

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "1. 기본 값 설정(동영상 크기: 맞춤, 자막: 끔, 볼륨: 50, 음소거: 켬, Windows 시작 시 자동 실행: 켬, 앱 시작 후 자동 재생: 켬, 자동 일시정지: 모두 켬) 2. 홈 화면에서 주소 입력한 값을 저장해서 앱을 다시 실행해도 계속 표시 함(재생은 계속 반복, 앱 종료 직전 홈에서 재생한 경우 앱 실행 시 자동 재생 하지 않음) 3. 홈 화면에 유튜브 로그인 정보 표시 4. 입력된 url로 재생을 못 하는 경우 예외 처리가 되어 있는지 확인, 다음 url로 재생 하는지 확인 5. 트레이 아이콘 수정(메뉴 스크롤 문제, 재생↔정지 문구 전환, 볼륨→음소거 문구)"
- **이해한 요구**: ① 앱 기본값 4건 변경(크기 맞춤·음소거 켬·앱 시작 자동 재생 켬·Windows 자동 실행 켬 — 볼륨 50·자막 끔·자동 일시정지 켬은 이미 기본값이라 무변경, 신규 설치에만 적용), ② 홈 URL 입력값을 설정에 저장해 재실행 시 텍스트박스에 복원 표시(반복 재생은 기구현 확인) + 마지막 재생이 홈 즉시 재생이면 일반 실행·부팅 모두 자동 재생 생략, ③ 홈 화면에 로그인 여부 상태 + 로그인/로그아웃 버튼(계정 이름·사진은 제외 — 질문 확정), ④ 재생 실패 처리 확인 결과 보고 + 발견된 스킵 공백(곡 시작 직후 에러 시 스킵 미발화)을 이번에 수정, ⑤ 트레이 메뉴를 PopupMenu 모드로 전환(스크롤 근본 해결)하고 재생/정지·음소거를 상태 연동 문구의 토글 2항목으로 재구성.
- **포함하지 않는 것으로 이해**: 계정 이름·프로필 사진 표시(비공식 스크래핑 필요 — 질문에서 "로그인 여부만"으로 확정), 기존 설치 PC의 저장된 설정 강제 변경(질문에서 "신규 설치만"으로 확정).

## Goal
새 설치 기본값을 사용자 지정값으로 맞추고, 홈 즉시 재생 UX(URL 기억·자동 재생 예외·로그인 상태)와 재생 실패 내성·트레이 메뉴 사용성을 개선한다.

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-1 (보강: 홈 URL 영속 표시·스킵 강화) | Must | T1, T3, T6 | ✅ 커버 |
| FR-5 (보강: 음소거 기본 켬) | Must | T1, T2 | ✅ 커버 |
| FR-8 (보강: 자동 실행 기본 켬 + 홈 재생 예외) | Must | T1, T2, T4 | ✅ 커버 |
| FR-9 (보강: 트레이 메뉴 토글 2항목·상태 연동 문구) | Must | T1, T7 | ✅ 커버 |
| FR-15 (보강: 홈 로그인 상태 표시) | Should | T1, T5 | ✅ 커버 |
| FR-16 (변경: 기본 맞춤) | Must | T1, T2 | ✅ 커버 |
| FR-19 (변경: 기본 켬 + 홈 재생 예외) | Must | T1, T2, T4 | ✅ 커버 |
| FR-2~4, 6, 7, 10~14, 18, 20, NFR-1~6 | Must/Should | (없음) | 이번 범위 외 (기구현/후속) |

## Out of Scope
- 계정 이름·이메일·프로필 사진 표시 (유튜브 비공식 스크래핑 필요 — ToS·취약성, 질문 확정)
- 기존 설치(이미 저장된 settings.json)의 값 강제 마이그레이션 (질문 확정 — 신규 설치만)
- 재생 위치(초 단위) 복원 (FR-19 기존 결정 유지 — 항목 단위 재개)

## Deferred / Follow-up
- [MINOR] AccountPanelViewModel에 Detach()가 없음(내부 이벤트 구독이 없어 실질 누수 없음 — spec 리뷰 M1 확인). 향후 패널이 외부 이벤트를 구독하게 되면 MonitorPanel처럼 Detach 대칭 추가 (출처: T5 spec MINOR)

## Investigation Log
- 위키 참조: 관련 위키 자료 없음 — 코드 1차 출처로 진행 (직전 plan들에서 vault 무매칭 이력, 프로젝트 국소 변경).
- Deferred 대장 확인(docs/plans/deferred.md ## 대기): "[2026-07-17] 트레이 메뉴 재생도 마지막 항목부터 재개" — 이번 T7(트레이 재작성)과 직접 관련 → **재수용**(T7에 1줄 포함). 그 외 항목은 이번 작업과 무관 — 대장 유지.
- 이전 plan(2026-07-17-captions-toggle) Deferred: "(없음)" + Phase G 통과 마커 확인 — 이관 잔여 없음.
- `AppSettings.cs` 전문 Read: 현재 기본값 — Volume=50 ✓, CaptionsEnabled=false(끔) ✓, PauseOn* 3종 true ✓ (요청과 이미 일치, 무변경). FitMode=Cover(채움)·IsMuted=false·AutoPlayOnLaunch=false → 변경 대상. Normalize()의 FitMode 폴백도 Cover(69행) — 기본값과 함께 변경.
- `FitMode.cs`: Cover(채움)/Contain(맞춤)/Stretch(늘리기) — resw `Fit_Contain`="맞춤" 확인. 요청 "맞춤" = `FitMode.Contain`.
- StartupTask: `Package.appxmanifest:44-48` `uap5:StartupTask Enabled="false"` — MSIX 패키지형 데스크톱 앱(runFullTrust)은 `Enabled="true"` 지정 시 설치 후 첫 실행부터 자동 활성화 가능(Microsoft Desktop Bridge 문서·Windows 개발자 블로그 확인). `StartupService.SetEnabledAsync`(사용자 토글)는 무변경으로 공존.
- 홈 URL 경로(`HomeViewModel.cs:156-211` PlayAsync): URL을 "빠른 재생"(이름 고정, resw `Home_QuickPlaylistName`) 플레이리스트의 단일 항목으로 교체 저장 후 `StartAsync(playlist.Id)` — **URL 자체는 영속 표시용으로 저장되지 않음**(Url 프로퍼티는 세션 한정, 생성자에서 string.Empty). 반복 재생은 기구현: `PlaybackQueue.Next()`가 전 모드 순환(1곡 리스트 `(0+1)%1=0` 무한 반복, FR-7) — 요청 "재생은 계속 반복"은 확인 종결.
- 자동 재생 경로(`App.xaml.cs:131-135, 139-158`): `if (autoPlay || AutoPlayOnLaunch) TryAutoPlayLastAsync()` — 부팅(autoPlay)·일반 실행 공용 단일 경로. `LastPlaylistId`는 StartAsync가 기록(`PlaybackCoordinator.cs:156-157`)하므로 홈 즉시 재생도 기록됨 → TryAutoPlayLastAsync 초입 한 곳에 "빠른 재생 리스트면 생략" 분기를 넣으면 부팅·일반 모두 적용(질문 확정과 정합).
- 로그인 인프라: `YouTubeSessionService`(SAPISID 쿠키로 로그인 여부만 판별, 계정 정보 미취득 — 클래스 주석 명시), 설정 화면 `AccountCard`(SettingsPage.xaml:145-159) + `SettingsViewModel.RefreshSessionAsync/AccountActionAsync`(:280-331) + `SignInRequested` 이벤트 → SettingsPage.xaml.cs:40-55에서 LoginWindow 열기. **계정 이름·사진 취득 코드는 레포에 없음**(전수 grep 확인).
- 재생 실패 처리(요청 4 확인 결과): ① URL 파싱 실패 — `YouTubeUrlParser.Parse`가 Result 실패 반환, Home/Playlists VM이 토스트 안내(저장 안 됨) ✓ ② 재생 불가 영상 — player.html onError → PlayerHost → `PlaybackCoordinator.OnPlayerError`(:409-434)가 마스터 기준 AdvanceAsync 스킵 ✓ ③ **공백**: 스킵 가드가 `!_suppressEnded`인데 `LoadAll`(:490)이 로드 시마다 `_suppressEnded=true` 설정 후 Playing 도달 시에만 해제(:398-401) → 곡 시작 직후 에러(첫 곡부터 재생 불가, 연속 재생 불가 곡)는 스킵 미발화. 기존 테스트(`임베드_금지_오류는_다음_곡으로_스킵한다`:255-266)는 `RaiseState(Playing)` 후 에러만 검증 — 공백 미커버 확정.
- 트레이(`TrayIconService.cs`): `ContextMenuMode.SecondWindow`(:65) — H.NotifyIcon 2.4.1(csproj:46). 스크롤·크기 오계산은 SecondWindow 모드의 알려진 버그(GitHub issue #21, preview 단계 명시), 코드에서 메뉴 항목 변경 미반영도 SecondWindow 한정 버그(issue #97 — PopupMenu·ActiveWindow는 정상 확인). PopupMenu(기본) 모드는 MenuFlyout 기반 Win32 네이티브 메뉴 생성 — 스크롤 구조 자체가 없고 `ContextMenuThemeMode` 시스템 테마 추종. 현재 볼륨 체크 동기화가 `menu.Opening`(:59) 의존 — 상태 이벤트 기반으로 전환해 Opening 발화 여부 의존 제거.
- 문구 갱신용 이벤트: `PlaybackCoordinator.StatusChanged`(:76)·`MutedChanged`(:79) 기존 존재 — 구독+디스패처 마셜링 선례는 `MonitorPanelViewModel`(:50-51, :162)·HomeViewModel(:116).
- 기본값 반전의 테스트 영향: `PlaybackCoordinatorTests` Harness FakeStore가 `new AppSettings()` 사용(:92) — IsMuted 기본 true가 되면 오디오 라우팅 단언 `mute:False` 3곳(:159, :303, :311)이 깨짐 → Harness 설정에 `IsMuted = false` 명시 초기화 필요. `JsonStateStoreTests:134` `Assert.False(AutoPlayOnLaunch)` 기본값 단언 반전 필요. PowerPolicyServiceTests는 PauseOn*만 사용(무변경) — 영향 없음.
- 저장 패턴: 설정 필드 변경 후 저장은 `_services.Store.SaveSettingsAsync(_services.Settings)`(SettingsViewModel:443, 460, 482 선례).

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| PopupMenu 모드 시각 변화(Win32 네이티브 메뉴) | 메뉴 모양이 WinUI 스타일에서 시스템 기본으로 바뀜 | 질문 라운드에서 사용자 확정(트레이 앱 관례 모양). HUMAN-VERIFY로 실표시 확인 |
| manifest `Enabled="true"`가 기존 설치 업데이트에 미적용 | 업데이트 설치는 Windows가 기존 StartupTask 상태 유지(표준 동작) — 신규 설치만 자동 켬 | 질문 확정(신규 설치만)과 정합 — 완료 보고에 명시 |
| PopupMenu 모드에서 메뉴 열림 시점 문구 신선도 | Opening 이벤트 발화가 모드별로 불확실 | Opening 의존 제거 — StatusChanged/MutedChanged 이벤트 시점에 항목 Text 선갱신(열기 전 항상 최신) |
| 홈·설정 페이지가 각자 계정 패널 인스턴스 보유 | 한쪽에서 로그인 후 다른 쪽이 stale 표시 | 페이지 재진입 시 Load→Refresh로 갱신(MonitorPanel과 동일 수용 기준 — 캐시 페이지 패턴) |
| 트레이 문구 갱신의 스레드 | StatusChanged/MutedChanged 발생 스레드 비보장 — UI 객체 접근 크래시 | Initialize(UI 스레드)에서 DispatcherQueue 캡처 → TryEnqueue 마셜링(MonitorPanelViewModel 선례) |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
| 심볼 | 영향 받는 파일 | 영향 종류 |
|---|---|---|
| `AppSettings.FitMode/IsMuted/AutoPlayOnLaunch` 기본값 | `Models/AppSettings.cs`(정의+Normalize), `tests/JsonStateStoreTests.cs`(:134 기본값 단언), `tests/PlaybackCoordinatorTests.cs`(Harness `new AppSettings()` → mute 단언 :159·:303·:311) | 값 변경 + 테스트 보정 (VM Populate는 저장값 반영이라 무변경) |
| `uap5:StartupTask@Enabled` | `Package.appxmanifest` | 속성값 변경 (StartupService·SettingsViewModel 로직 무변경) |
| `AppSettings.LastHomeUrl` (신규) | `Models/AppSettings.cs`, `ViewModels/HomeViewModel.cs`, `tests/JsonStateStoreTests.cs` | additive 필드 (JSON 하위 호환 — null 기본, 선례 다수) |
| `App.TryAutoPlayLastAsync` | `App.xaml.cs` (호출부 1곳 — InitializeServicesAsync 내부) | 내부 분기 추가 (시그니처 불변) |
| SettingsViewModel 계정 심볼(`AccountStatusText/AccountButtonText/AccountActionAvailable/AccountActionCommand/SignInRequested/RefreshSessionAsync/_signedIn/_session`) | `ViewModels/SettingsViewModel.cs`, `Views/SettingsPage.xaml`(:145-159 x:Bind), `Views/SettingsPage.xaml.cs`(:27-55 구독·LoginWindow) | 공용 VM으로 이동 — 참조처 전수 갱신 (grep 확인: 이 3파일 외 참조 없음) |
| `PlaybackCoordinator.OnPlayerError/_suppressEnded` | `Services/PlaybackCoordinator.cs`, `tests/PlaybackCoordinatorTests.cs` | 에러 스킵 가드 분리 + 테스트 추가 (공개 API 불변) |
| `TrayIconService` 메뉴 구성·`Tray_Volume` resw 키 | `Services/TrayIconService.cs`, `Strings/ko-KR·en-US/Resources.resw` | 재작성 + 키 교체 (grep: Tray_Volume 참조는 TrayIconService:44 1곳뿐) |
| `PlaybackCoordinator.StatusChanged/MutedChanged` | (구독 추가만 — TrayIconService) | additive 구독 (기존 구독자 HomeViewModel·MonitorPanelViewModel 무변경) |

### 4-B. 계약·직렬화 변경
- `AppSettings.LastHomeUrl`(string?) additive — 구형 JSON 로드 시 null(하위 호환), 선례: LastItemId·LastSelectedPlaylistId.
- 기본값 변경은 직렬화 형식 불변 — 기존 파일에 필드가 이미 기록돼 있으면 그 값 유지(신규 설치·필드 부재 시만 새 기본값). SchemaVersion 불변.
- 공개 API 시그니처 변경 없음. `AccountPanelViewModel` 신설은 additive(기존 SettingsViewModel 공개 계정 멤버는 페이지 전용 — 외부 소비자는 SettingsPage뿐임을 grep 확인).

### 4-C. 테스트 파일
- `tests/DeskTube.Tests/JsonStateStoreTests.cs` — 기본값 단언 갱신(FitMode·IsMuted·AutoPlayOnLaunch) + LastHomeUrl 왕복·구형 JSON null
- `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` — Harness IsMuted 명시 초기화 + 에러 스킵 신규 3종
- VM(HomeViewModel)·App·TrayIconService는 테스트 인프라 부재(서비스 계층만 테스트하는 프로젝트 관례) — HUMAN-VERIFY

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `AccountPanelViewModel` | 계정 로직은 SettingsViewModel에 1벌 존재(:280-331) — 홈 표시 요구로 2번째 소비자 발생 | 공통화 규칙(반복 2회) 충족 — SettingsViewModel에서 추출해 공용화. 구조 선례는 `MonitorPanelViewModel`(홈·설정 공유 패널 VM) |
| `AppSettings.LastHomeUrl` | grep `LastHomeUrl` 0건, 유사 필드 LastPlaylistId/LastItemId는 재생 이력(용도 다름) | 신규 — 입력값 저장은 재생 이력과 별개(질문 확정 D3) |
| 에러 스킵 카운터(`_errorSkipCount` 등 내부 필드) | `_suppressEnded` 기존 가드는 Ended 중복 억제 용도 — 에러 경로와 공유가 공백의 원인 | 신규 내부 필드 — 목적 분리가 근본 해결(D7) |
| resw `Tray_Mute/Tray_Unmute`, `Playback_AllItemsFailed` | 기존 키 grep — 음소거 문구는 `Settings_*` 계열뿐(트레이 전용 없음), 전곡 실패 안내 없음 | 신규 키 (Tray_Play/Tray_Stop·Settings_Account*는 재사용) |
| 홈 로그인 상태 UI | 기존 `AccountCard`(SettingsCard)는 설정 전용 스타일 — 홈은 제목 행 우측 경량 표시 | 기존 토큰 브러시·버튼 스타일 재사용, 신규 컨트롤 없음 |

### Verified by
- grep `FitMode.Cover|IsMuted|AutoPlayOnLaunch` (*.cs 전체) → 27 hits, 전건 문맥 확인 — 기본값 의존은 위 표 3파일뿐(그 외는 저장값 경유라 무변경)
- grep `Tray_Volume` → 3 hits(정의 ko/en + TrayIconService:44) — 전건 T7 범위
- grep `Account\w+|SignInRequested` → SettingsViewModel·SettingsPage(.xaml/.cs) 3파일 — 전건 T5 범위
- grep `StatusChanged|MutedChanged` → 발화 Coordinator, 구독 HomeViewModel·MonitorPanelViewModel(무변경) — T7은 구독 추가만

## Decisions
### D1. 기본값 적용 범위
- **Options**: A) 신규 설치만(필드 부재 시 기본값) / B) SchemaVersion 마이그레이션으로 기존 파일 강제
- **Chosen**: A
- **Rationale**: 표준 동작 — 저장된 사용자 선택 존중. 개발 PC 확인은 settings.json 삭제로 가능.
- **Source**: 질문 라운드 1 사용자 확정.

### D2. Windows 시작 시 자동 실행 기본 켬 방식
- **Options**: A) manifest `uap5:StartupTask Enabled="true"` / B) 최초 실행 시 RequestEnableAsync 자동 호출 + 1회 시도 플래그
- **Chosen**: A
- **Rationale**: 패키지형 데스크톱 앱은 manifest 지정으로 설치 시점부터 기본 켬(공식 메커니즘, 코드 0줄). B는 플래그 필드 추가·타이밍 이슈. 사용자가 Windows 설정에서 끄면 그 선택이 유지됨(DisabledByUser — StartupService 기존 방어 로직 그대로).
- **Source**: Microsoft Desktop Bridge StartupTask 문서(Enabled=true는 desktop 앱 허용) + StartupService.cs:37 주석(동의 창 없이 즉시 적용).

### D3. 홈 URL 저장 위치
- **Options**: A) AppSettings.LastHomeUrl 신설 / B) "빠른 재생" 리스트 항목에서 복원
- **Chosen**: A
- **Rationale**: 요청 원문("입력한 값을 저장")에 충실 + 사용자가 빠른 재생 리스트를 삭제해도 표시 유지. B는 리스트 상태에 표시가 종속.
- **Source**: 질문 라운드 2 사용자 확정.

### D4. 홈 재생 자동 재생 제외의 식별·적용 범위
- **Options**: A) TryAutoPlayLastAsync 초입에서 LastPlaylistId가 "빠른 재생" 리스트(이름 = `Loc.Get("Home_QuickPlaylistName")`)면 생략 / B) AppSettings에 "마지막 재생이 홈" bool 저장(전 재생 진입점 갱신 필요)
- **Chosen**: A — 부팅·일반 실행 모두 적용(TryAutoPlayLastAsync가 두 경로 공용이라 한 곳 수정으로 충족)
- **Rationale**: 국소 수정 1곳. 이름 기반 식별은 HomeViewModel의 기존 리스트 식별 방식과 동일 수준(동일 한계 공유). B는 StartAsync 호출부 전체(홈·플레이리스트·트레이·App) 갱신 필요 — 과도.
- **Source**: 질문 라운드 2 확정(부팅에도 적용) + HomeViewModel.cs:174-175(이름 식별 선례).

### D5. 홈 로그인 표시 수준·구조
- **Options**: A) 로그인 여부 + 로그인/로그아웃 버튼, 계정 로직을 `AccountPanelViewModel`로 공용 추출 / B) 계정 이름·사진까지(스크래핑) / C) 홈엔 표시만 하고 버튼은 설정 이동
- **Chosen**: A
- **Rationale**: 기존 인프라(YouTubeSessionService·LoginWindow) 재사용, 반복 2회 규칙 충족으로 공용 VM 추출 정당(MonitorPanelViewModel 선례 동형). B는 비공식·취약.
- **Source**: 질문 라운드 1 확정 + AGENTS.md Conventions(공통화 기준).

### D6. 트레이 메뉴 방식
- **Options**: A) ContextMenuMode.PopupMenu 전환 + 토글 2항목(재생/정지, 음소거/음소거 해제) + 상태 이벤트 기반 문구 갱신 / B) SecondWindow 유지 + 크기·갱신 우회
- **Chosen**: A
- **Rationale**: 스크롤·동적 갱신 미반영 모두 SecondWindow preview 버그(issue #21/#97) — 모드 전환이 원인 제거. 문구 갱신은 Opening 이벤트 의존 대신 StatusChanged/MutedChanged 구독으로 선갱신(모드 무관 견고).
- **Source**: 질문 라운드 1 확정(모드·토글 2항목 모두) + H.NotifyIcon README·issue #97(PopupMenu 동적 변경 정상).

### D7. 재생 불가 스킵 가드 분리
- **Options**: A) 에러 전용 상태(연속 에러 카운터 + 에러 스킵 pending 플래그)로 `_suppressEnded`와 분리, 전곡 재생 불가 시 정지+토스트 / B) `_suppressEnded` 가드 완화(에러는 무조건 스킵)
- **Chosen**: A
- **Rationale**: B는 같은 곡의 중복 에러 이벤트로 다중 스킵(연쇄 건너뜀)·전곡 재생 불가 시 무한 재로드 루프 위험. A는 중복 방지와 종료 조건(카운터 ≥ 큐 항목 수 → StopAsync + 안내 토스트)을 함께 해결.
- **Source**: PlaybackCoordinator.cs:398-434 가드 구조 분석 + 질문 라운드 2 확정(이번에 수정).

### D8. 트레이 재생의 항목 재개 (Deferred 재수용)
- **Options**: A) `StartAsync(playlist.Id, settings.LastItemId)`로 1줄 변경 / B) 이번에도 보류
- **Chosen**: A
- **Rationale**: T7이 같은 메서드(PlayAsync)를 수정하므로 재수용 비용 최소. 항목이 리스트에 없으면 PlaybackQueue.Start가 무시하고 기본 시작(기존 테스트 보장 — FR-19 Edge와 동일).
- **Source**: deferred.md 대기 항목(2026-07-17, autoplay-on-launch 출처) + App.TryAutoPlayLastAsync 동형 선례.

## Tasks
- [x] T1. PRD 갱신 — 기본값·홈 재생 예외·트레이 문구·홈 로그인 표시 반영
  - **Type**: A
  - **Acceptance**: FR-16 기본값 "맞춤", FR-19 "기본 켜짐 + 마지막 재생이 홈 즉시 재생이면 생략", FR-5 "음소거 기본 켬", FR-8 "자동 실행 기본 켬(manifest) + 홈 즉시 재생 예외(FR-19와 동일)", FR-9 "재생/정지 토글·음소거/음소거 해제 토글(상태 연동 문구), 설정 열기, 종료", FR-15 "홈 화면에 로그인 상태 표시", FR-1 "홈 URL 입력값 저장·재실행 시 표시 + 재생 불가 곡 연속 스킵·전곡 재생 불가 시 정지·안내" 반영 + 변경 이력 1줄 추가. 폐기·타 FR 문구 불변.
  - **Files**:
    - 주: `docs/prd.md`
  - **Edge Cases**: (Type A — skip)
  - **Depends on**: -
- [x] T2. 기본값 변경 — AppSettings 3종 + StartupTask manifest
  - **Type**: C
  - **Acceptance**: Given 설정 파일 없음, When LoadSettingsAsync, Then FitMode=Contain·IsMuted=true·AutoPlayOnLaunch=true (JsonStateStoreTests 기본값 테스트로 검증). Normalize의 FitMode 폴백도 Contain. manifest StartupTask `Enabled="true"`. 기존 오디오 라우팅 테스트는 Harness IsMuted=false 명시 초기화로 의미 유지(전 테스트 통과).
  - **Files**:
    - 주: `src/DeskTube/Models/AppSettings.cs` (기본값 3곳 + Normalize 폴백 + 주석 갱신 — "기본 끔/꺼짐" 표기 반전)
    - 동반: `src/DeskTube/Package.appxmanifest` (uap5:StartupTask Enabled="true" — BOM 유지, VS 관례)
    - 테스트: `tests/DeskTube.Tests/JsonStateStoreTests.cs` (기본값 단언 반전+FitMode·IsMuted 단언 추가), `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` (Harness 설정 IsMuted=false 명시 — 라우팅 테스트가 비음소거 전제임을 주석으로)
  - **Edge Cases**:
    - 기존 settings.json에 필드가 이미 기록된 사용자 — 기존 값 유지(D1, 의도된 동작)
    - 손상 JSON 폴백(`손상된_JSON은_bak으로_보존` 테스트) — 새 기본값으로 시작(자동 충족)
    - 업데이트 설치는 StartupTask 기존 상태 유지(Windows 표준) — 신규 설치만 자동 켬
  - **Halt Forecast**:
    - (i) manifest 스키마 검증 실패 가능성 → 빌드가 manifest 검증 포함(dotnet build로 즉시 검출), Enabled 속성은 uap5:StartupTask 스키마에 존재(현재 파일에 이미 `Enabled="false"`로 사용 중 — 값만 변경)
  - **Depends on**: T1
- [x] T3. 홈 URL 저장·복원 표시
  - **Type**: C
  - **Design**: ① `AppSettings.LastHomeUrl`(string?, additive)에 저장, 소비는 HomeViewModel만 ② 신규 심볼: LastHomeUrl 필드 1개(4-D 표) ③ HomeViewModel → Store.SaveSettingsAsync(기존 저장 패턴) ④ 별도 서비스·이벤트 도입 안 함(표시 복원은 페이지 진입 시 1회 읽기로 충분 — YAGNI).
  - **Acceptance**: Given 홈에서 URL 재생 성공, When 앱 재시작 후 홈 진입, Then URL 텍스트박스에 마지막 재생 URL 표시. Given 사용자가 입력 중(Url 비어 있지 않음), When 페이지 재진입, Then 입력 값 유지(복원이 덮지 않음). 저장 왕복·구형 JSON null 테스트 통과.
  - **Files**:
    - 주: `src/DeskTube/ViewModels/HomeViewModel.cs` (PlayAsync 성공 시 `LastHomeUrl = Url.Trim()` + SaveSettingsAsync, AttachCore에서 Url이 빈 값일 때만 복원)
    - 동반: `src/DeskTube/Models/AppSettings.cs` (필드 추가)
    - 테스트: `tests/DeskTube.Tests/JsonStateStoreTests.cs` (왕복 + 구형 JSON 기본 null)
  - **Edge Cases**:
    - 파싱 실패 URL은 저장 안 함(재생 성공 시에만 저장 — 기존 토스트 경로 무변경)
    - LastHomeUrl null/공백 — 복원 생략(빈 텍스트박스 유지)
    - 저장 실패(디스크) — 기존 SaveSettingsAsync Result 로그 패턴 따름, 재생은 이미 시작됨(무영향)
  - **Halt Forecast**: (i) 저장 시점에 Store 접근 경로 불명 → Investigation Log에 확인 완료(`_services.Store.SaveSettingsAsync` 선례 SettingsViewModel:443)
  - **Depends on**: T2
- [x] T4. 홈 즉시 재생은 자동 재생 대상에서 제외 (일반 실행·부팅 공통)
  - **Type**: B
  - **Acceptance**: Given LastPlaylistId가 "빠른 재생" 리스트, When 앱 시작(일반 실행 AutoPlayOnLaunch=true 또는 부팅 autoPlay), Then 자동 재생 생략 + AppLog 사유 기록. Given 마지막 재생이 일반 플레이리스트, Then 기존대로 자동 재생. 빌드 통과(로직은 App 계층 — 단위 테스트 인프라 없음, HUMAN-VERIFY).
  - **Files**:
    - 주: `src/DeskTube/App.xaml.cs` (TryAutoPlayLastAsync 초입 분기 — playlist 조회 후 `playlist.Name == Loc.Get("Home_QuickPlaylistName")`면 로그 후 return)
  - **Edge Cases**:
    - 사용자가 직접 "빠른 재생" 이름의 리스트를 만들어 재생한 경우도 제외됨 — HomeViewModel의 기존 이름 식별과 동일 한계(그 리스트는 홈이 점유·교체하므로 실질 동일 콘텐츠), 수용
    - 언어 전환으로 리스트 이름과 Loc 값이 어긋나는 경우 제외 미적용 가능 — 기존 HomeViewModel 동일 한계(새 언어로 새 리스트 생성), 수용·주석 기록
  - **Halt Forecast**: (없음 — 단일 파일 국소 분기, 파괴적·외부 요소 없음)
  - **Depends on**: T3
- [x] T5. 계정 패널 공용화 + 홈 로그인 상태 표시
  - **Type**: D
  - **Design**: ① `ViewModels/AccountPanelViewModel.cs` 신설 — SettingsViewModel의 계정 로직(RefreshSessionAsync·AccountActionAsync·상태/버튼 프로퍼티·SignInRequested 이벤트) 이동, MonitorPanelViewModel과 동형의 공유 패널 VM(각 소유 VM이 `public AccountPanelViewModel Account { get; } = new()` 보유, Attach(services)/Detach 대칭) ② 책임: 로그인 상태 조회·표시 문구·로그인 요청 발화·로그아웃 실행(ReloadCurrentTrack 포함) ③ 의존: YouTubeSessionService·PlaybackCoordinator(AppServices 경유) — 참조자는 SettingsViewModel·HomeViewModel ④ 비추상화 선언: 인터페이스 추출·상태 공유 이벤트 버스 도입 안 함(두 페이지 인스턴스는 각자 Refresh — 캐시 페이지 재진입 갱신으로 충분).
  - **Acceptance**: Given 홈 화면 진입, When 로그인 상태 확인 완료, Then 제목 행 우측에 "로그인됨/로그인 안 됨" 상태 + 로그인/로그아웃 버튼 표시, 버튼 클릭 → LoginWindow(로그인) 또는 로그아웃+상태 갱신. 설정 화면 계정 카드는 기존과 동일 동작(위임 전환 후 회귀 없음 — 문구·버튼·비활성 방어 유지). 빌드 경고 0, 기존 테스트 전체 통과. 실표시는 HUMAN-VERIFY.
  - **Files**:
    - 주: `src/DeskTube/ViewModels/AccountPanelViewModel.cs` (신규)
    - 동반: `src/DeskTube/ViewModels/SettingsViewModel.cs` (계정 로직 제거·Account 패널 위임), `src/DeskTube/Views/SettingsPage.xaml` (x:Bind 경로 `ViewModel.Account.*`), `src/DeskTube/Views/SettingsPage.xaml.cs` (SignInRequested 구독 경로 변경), `src/DeskTube/ViewModels/HomeViewModel.cs` (Account 보유 + Attach/Detach), `src/DeskTube/Views/HomePage.xaml` (제목 행을 2열 Grid로 — 우측에 상태 텍스트+버튼, 기존 토큰 브러시 사용), `src/DeskTube/Views/HomePage.xaml.cs` (SignInRequested → LoginWindow, SettingsPage 선례 복제)
    - resw: 기존 `Settings_Account*`·`Settings_SignIn/SignOut` 키 재사용 (신규 키 0 — 홈 표시는 VM 바인딩 문구)
  - **Edge Cases**:
    - 세션 확인 실패(WebView2 런타임 문제) — 버튼 비활성 방어(기존 로직 이동으로 유지)
    - 캐시 페이지 재진입 — Loaded/Unloaded 대칭 구독(SettingsPage 주석의 기지 함정: ctor 구독+Unloaded 해제면 2번째 진입부터 무반응)
    - 홈에서 로그인 → 설정 페이지 stale — 설정 재진입 시 Load→Refresh로 갱신(수용 기준, Risks 표)
    - 로그인 창을 도중에 닫음 — 상태 변화 없음(기존 Closed→Refresh 패턴 유지)
  - **Halt Forecast**:
    - (ii-a) 구조 변경(계정 로직을 SettingsViewModel → 신규 AccountPanelViewModel로 이동, SettingsViewModel 공개 멤버 제거) → `## 사전 승인 항목`에 등록
  - **Depends on**: T2
- [ ] T6. 재생 불가 스킵 가드 분리 + 전곡 재생 불가 정지
  - **Type**: C
  - **Design**: ① PlaybackCoordinator 내부 필드 2개 — 연속 에러 카운터(Playing 도달 시 0 리셋)·에러 스킵 pending 플래그(중복 에러 이벤트 무시용, LoadAll에서 해제) ② OnPlayerError의 스킵 조건을 `_suppressEnded` 공유에서 분리(D7) ③ 카운터가 현재 큐 항목 수 이상이면 StopAsync + `ToastService.Show`(전곡 재생 불가 안내 — ToastService는 마셜링 내장·미등록 무시로 서비스 계층 호출 안전) ④ 비추상화: 에러 정책 클래스 분리 안 함(코디네이터 국소 상태 2개로 충분).
  - **Acceptance**: Given 재생 시작 직후 첫 곡이 재생 불가(Playing 이전 onError), When 마스터 에러 수신, Then 다음 곡 로드(신규 테스트). Given 연속 2곡 재생 불가, Then 3곡째 로드(신규 테스트). Given 전 곡 재생 불가, Then StopAsync로 정지 + 안내 토스트(신규 테스트 — Status Stopped 단언). 기존 스킵·Ended 테스트 전체 통과(회귀 0).
  - **Files**:
    - 주: `src/DeskTube/Services/PlaybackCoordinator.cs`
    - 동반: `src/DeskTube/Strings/ko-KR/Resources.resw`·`src/DeskTube/Strings/en-US/Resources.resw` (신규 키 `Playback_AllItemsFailed` — ko "재생할 수 있는 항목이 없어 재생을 중지했습니다." / en "Playback stopped because no items could be played.")
    - 테스트: `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` (신규 3종)
  - **Edge Cases**:
    - 1곡 리스트가 재생 불가 — 1회 재시도 후 카운터=1 ≥ 항목 수 1 → 정지+안내(무한 재로드 방지)
    - 같은 곡에서 에러 이벤트 중복 발화 — pending 플래그로 1회만 스킵
    - -1(API 로드 실패)·-2(프로세스 실패)는 기존 별도 경로(재시도·재생성) 불변
    - 재생 중 사용자가 정지 — Status Stopped 조기 반환(기존 가드 유지)
    - Ended 정상 진행 경로(`_suppressEnded`)는 기존 동작 불변(가드 분리의 목적)
  - **Halt Forecast**: (i) 큐 항목 수 접근 방법 → PlaybackQueue에 Count 노출 여부 구현 시 확인 — 없으면 큐 생성 시점의 항목 수를 코디네이터가 보관(둘 다 내부 구현, 계약 불변이라 성립에 영향 없음)
  - **Depends on**: T2
- [ ] T7. 트레이 메뉴 재작성 — PopupMenu 모드 + 토글 2항목 + 항목 재개
  - **Type**: C
  - **Design**: ① TrayIconService 내부 재구성(파일 이동 없음) — 메뉴 [재생|정지 토글][음소거|음소거 해제 토글][구분선][설정 열기][종료] ② 신규 심볼 없음(내부 필드 `_playStopItem`·`_muteItem`·`_dispatcher`) ③ Coordinator.StatusChanged·MutedChanged 구독(Initialize에서 DispatcherQueue 캡처 → TryEnqueue로 Text 갱신, Dispose에서 구독 해제) ④ 비추상화: 메뉴 상태 VM 분리 안 함(서비스 내 국소 갱신으로 충분), Opening 이벤트 의존 제거.
  - **Acceptance**: Given 정지·일시정지 상태, Then 첫 항목 문구 "재생"(클릭 시 재개/마지막 리스트 재생 — 마지막 항목부터, D8), Given 재생 중, Then "정지"(클릭 시 정지). Given 비음소거, Then "음소거"(클릭 시 음소거), Given 음소거, Then "음소거 해제". ContextMenuMode=PopupMenu. Tray_Volume 키 잔존 0(grep). 빌드 경고 0 — 메뉴 실표시(스크롤 없음·문구 전환)는 HUMAN-VERIFY.
  - **Files**:
    - 주: `src/DeskTube/Services/TrayIconService.cs`
    - 동반: `src/DeskTube/Strings/ko-KR/Resources.resw`·`src/DeskTube/Strings/en-US/Resources.resw` (신규 `Tray_Mute` ko "음소거"/en "Mute", `Tray_Unmute` ko "음소거 해제"/en "Unmute" — `Tray_Volume` 삭제, `Tray_Play`/`Tray_Stop` 재사용)
  - **Edge Cases**:
    - 초기 문구 — Initialize 시 현재 Status·IsMuted 반영(이벤트 대기 없이)
    - StatusChanged/MutedChanged 발생 스레드 비보장 — 디스패처 마셜링(Risks 표)
    - Paused 상태 클릭 — 기존 PlayAsync의 Resume 경로 유지. 토글 2항목 설계상 일시정지 중엔 트레이에서 직접 "정지" 불가(문구가 "재생") — 사용자 확정(Q4) by-design, HUMAN-VERIFY에서 인지 확인 (plan 리뷰 m1)
    - 재생할 리스트 없음/시작 실패 — 기존 설정 창 안내 경로 불변
    - LastItemId가 리스트에 없음 — PlaybackQueue.Start가 무시하고 기본 시작(기존 테스트 보장)
    - Dispose 후 늦게 도착하는 이벤트 — 구독 해제 + null 가드
  - **Halt Forecast**: (i) PopupMenu 모드에서 ToggleMenuFlyoutItem 체크 표시 지원 불확실 → 체크 미사용 설계로 회피(문구가 상태 표현 — D6), 항목은 일반 MenuFlyoutItem로 전환
  - **Depends on**: T6 (resw 같은 파일 순차 수정)
- [ ] T8. README 갱신
  - **Type**: A
  - **Acceptance**: 기본값 표(맞춤·음소거 켬·자동 실행 켬·자동 재생 켬), 홈 URL 기억·자동 재생 예외, 홈 로그인 상태 표시, 재생 불가 연속 스킵·전곡 정지, 트레이 메뉴 구성(토글 2항목) 반영 — 존재하지 않는 기능 기재 0.
  - **Files**:
    - 주: `README.md`
  - **Edge Cases**: (Type A — skip)
  - **Depends on**: T7

## 사전 승인 항목 (일괄 승인 대상)
- T2 — `Package.appxmanifest` StartupTask `Enabled="false"→"true"` (앱 패키지 매니페스트 설정 변경 — 신규 설치의 자동 실행 기본 상태를 바꿈, 되돌리기는 값 원복)
- T5 — 구조 변경: SettingsViewModel의 계정 로직을 신규 `AccountPanelViewModel`로 추출·이동(SettingsViewModel 공개 계정 멤버 제거 — 참조자는 SettingsPage뿐임을 전수 확인)
- T7 — resw 키 삭제(`Tray_Volume` ko/en — 참조 1곳이 같은 task에서 교체됨)

## 불가피한 Halt (위임 불가)
- push·main 병합·태그·릴리즈·PR (구현·검증 완료 후 최종 보고에서 별도 승인)

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` (경고 0·오류 0)
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -p:Platform=x64` (⚠ `-p:Platform=x64` 필수 — 미지정 시 MSIX AnyCPU 에러, Deferred 대장 기지)
- 포맷: `dotnet format` (위반 0)
- 수동 검증 (HUMAN-VERIFY): ① 설정 파일 삭제 후 실행 → 기본값 확인(맞춤·음소거·자동 재생) ② 신규 설치 후 Windows 시작 앱에 데스크튜브 켬 상태 ③ 홈 URL 재생 → 재시작 → URL 표시 + 자동 재생 안 됨 ④ 플레이리스트 재생 → 재시작 → 자동 재생 됨 ⑤ 홈 로그인 상태·버튼 동작 ⑥ 재생 불가 영상 스킵·전곡 불가 정지 토스트 ⑦ 트레이 메뉴 스크롤 없음·문구 전환·음소거 토글·마지막 항목 재개 (참고: 일시정지 중엔 토글이 "재생"이라 트레이 직접 정지는 불가 — 토글 2항목 설계의 의도된 동작)

## Phase Ledger

## Retry Ledger

## Progress Log
- T3-T4 완료 (커밋 ee03c55, 703abc8): LastHomeUrl 저장·복원(성공 시 기록, 빈 입력란만 복원) + TryAutoPlayLastAsync 빠른 재생 생략 분기. 테스트 108/108, 리뷰 첫 판 OK·prefilter PASS.
- T1-T2 완료 (커밋 ad37199, deaffa0): PRD 7건 보강 + 기본값 3종(Contain/음소거 켬/자동 재생 켬) + manifest StartupTask Enabled=true. 테스트 108/108, spec·quality OK.
  - 결정: 왕복 테스트 값은 기본값과 다르게 유지(직렬화 누락 감지), Harness IsMuted=false 고정(라우팅 테스트 전제), diff 밖 FR-19 stale 주석 2곳 동기화.

## Next Steps

## Open Questions
- [x] Q1: 새 기본값을 기존 저장 파일에도 강제? → **신규 설치만** (D1)
- [x] Q2: 홈 로그인 정보 수준? → **로그인 여부 + 로그인/로그아웃 버튼** (D5)
- [x] Q3: 트레이 해결 방식? → **PopupMenu 모드 전환** (D6)
- [x] Q4: 트레이 문구 구성? → **토글 2항목 (재생↔정지 / 음소거↔음소거 해제)** (D6)
- [x] Q5: 홈 재생 자동 재생 제외를 부팅에도? → **부팅에도 적용** (D4)
- [x] Q6: 스킵 공백 수정 포함? → **이번에 수정** (D7)
- [x] Q7: 홈 URL 저장 위치? → **AppSettings 필드 신설** (D3)
