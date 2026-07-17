# Plan: 성능 최적화 3건 — 정책 일시정지 서스펜션·볼륨 저장 디바운스·미러 캡 기본 켬

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "메모리/cpu 등의 사용률 최적화 검토" → 보고 후 "모두 수정"
- **이해한 요구**: 성능 검토에서 보고한 개선 기회 3건을 모두 구현한다 — ① 자동(정책) 일시정지 3종(전체화면·배터리 세이버·화면 잠금) 동안 WebView2를 서스펜션해 CPU·메모리 회수(질문 확정: 정책 일시정지 전체 즉시, 사용자 일시정지는 비대상 — 서스펜션 중 배경이 검게 보이는 것 고지·수용) ② 볼륨 슬라이더 드래그 시 settings.json 다중 쓰기를 디바운스(즉시 적용은 유지, 영속화만 지연) ③ 미러 모니터 화질 캡(720)을 신규 설치 기본 켬으로 전환(기존 사용자의 저장된 선택 유지 — 질문 확정, PRD·README 갱신 동반).
- **포함하지 않는 것으로 이해**: 사용자 일시정지 서스펜션(질문에서 A안 채택 — B안 기각), 관찰 항목(곡 전환 저장·playlists.json 전체 직렬화·AppLog IO 등 "조치 불요" 판정분)은 수정하지 않는다.

## Goal
일시정지·설정 조작·다중 모니터 재생에서 불필요한 CPU·메모리·디스크 사용을 줄인다 (기존 기능 동작 불변).

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| NFR-1 (보강: 정책 일시정지 중 서스펜션) | Must | T1, T4 | ✅ 커버 |
| NFR-2 (보강: 미러 캡 기본 켬) | Must | T3, T4 | ✅ 커버 |
| FR-5 (볼륨 영속 — 동작 불변, 저장 시점만 디바운스) | Must | T2 | ✅ 커버 (요구 문구 불변) |
| 나머지 active Must FR | Must | (기구현) | 이번 범위 외 (기구현/후속) |

## Out of Scope
- 사용자 일시정지 중 서스펜션 (질문 라운드 기각 — 일시정지 프레임이 검게 변하는 UX 회귀)
- 서스펜션 중 설정 변경 후 자동 재서스펜션 (드문 경로 — 다음 정책 일시정지에서 자연 회복)
- 성능 검토의 "관찰(조치 불요)" 항목 전부

## Deferred / Follow-up
- [T2 quality m1] `_volumeSaveCts` 미Dispose — CancelMetadataBackfill 관례(경합 방지, GC 수거) 승계로 판정. CTS 수명 관리 컨벤션을 바꾸게 되면 함께 정리
- [T2 quality m2] 볼륨 디바운스 테스트가 실시간 `Task.Delay(1200)` 의존 — 가상 시계 인프라 도입 시 전환 검토 (현재 여유폭 2.4배)

## Investigation Log
- 위키 참조: 관련 위키 자료 없음 — 코드 1차 출처로 진행 (vault 미설정 확인 — 세션 내 Test-Path)
- Deferred 대장 확인 — 관련 항목: "PowerPolicyService PowerManager 정적 이벤트 해제"(DI 도입 시 동반 — 이번 작업과 별개 유지). 그 외 무관.
- WebView2 공식 문서(context7 — learn.microsoft.com WebView2 레퍼런스) 직접 조회: `CoreWebView2.TrySuspendAsync()`는 **`CoreWebView2Controller.IsVisible == false` 선행 필수**(아니면 COMException), best-effort(bool 결과 — 실패 시 false·오류 아님), 스크립트 타이머·애니메이션 정지 + 렌더러 메모리 OS 반환. **IsVisible=true로 되돌리면 자동 재개**, 명시 `Resume()`도 제공. 서스펜션 중에도 모든 WebView API 접근 가능하나 일부(Navigate 등)는 자동 재개 유발 — `IsSuspended`로 확인 권장. IsVisible=false는 렌더링 중단(투명) — 배경창의 검정 브러시가 보인다.
- `PlaybackCoordinator.cs` 직접 Read (전문 — 이 세션) — PolicyPause(:234~241 부근)·Resume(:219~)·PolicyResume(:252~257)·ResumeOrSkipFailed(:267~277, Resume·PolicyResume **공용 재개 경로**), PauseAll/PlayAll, Dispose(:423), 볼륨/음소거(:279~292 — SetVolumeAsync가 매 호출 SaveSettingsAsync), EffectiveScaleFor(:379~) 확인.
- `PowerPolicyService.cs` 전문 Read — 정책 신호 3종(Fullscreen 2초 폴링·BatterySaver 이벤트·SessionLock WTS), PauseRequested/ResumeRequested는 사유 무구분 합성. 사유 구분 배선 불필요(질문 확정 A안 — 전체 적용).
- `PlayerHost.cs` 전문 Read — `_controller`(CoreWebView2Controller) 보유, PostCommand(:130~137)가 모든 명령 단일 경로, Dispose(:110~124). IPlayerHost 명령은 "fire-and-forget, 결과는 이벤트" 계약(IPlayerHost.cs:29).
- `IPlayerHost.cs` 전문 Read — 인터페이스 확장 시 구현체 전수: `PlayerHost`(실물)·`PlaybackCoordinatorTests.FakePlayer`(tests:62~88) 2곳뿐 (grep `IPlayerHost` 전수 — 다른 구현 없음).
- `SettingsViewModel.cs:321~322` — OnVolumeChanged가 슬라이더 값 변경마다 `SetVolumeAsync` 호출(fire-and-forget Apply). `SetVolumeAsync` 호출자 전수 grep: SettingsViewModel 1곳뿐.
- 볼륨 관련 테스트 전수 grep: `볼륨_변경은_오디오_대상에만_적용된다`(:348 — 명령 적용 단언, 저장 단언 없음·FakeStore no-op) → 디바운스가 즉시 반환하면 무영향. `범위_밖_볼륨은_로드_시_보정된다`(JsonStateStoreTests:173 — 무관).
- `AppSettings.cs:36` — `ReduceMirrorQuality` 기본 false. 직렬화는 기본값도 기록(StateJsonContext — WriteIndented만, ignore 없음) → **기존 사용자 settings.json에는 false가 명시 기록돼 있어 기본값 변경이 덮지 않는다**(FitMode·IsMuted 기본 변경과 동일 패턴 — 신규 설치·필드 미기록만 새 기본 적용).
- 미러 캡 테스트 2건 직접 Read(PlaybackCoordinatorTests:488~513) — :507은 명시 `= true`(기본 반전 무영향), :494 테스트는 "기본 꺼짐에서 시작 → 토글 켬" 의도라 Harness 기본이 true가 되면 의도 훼손 → **Harness에 `ReduceMirrorQuality = false` 명시 고정**(IsMuted 선례 :125)으로 기존 테스트 의도 보존.
- 왕복 테스트(JsonStateStoreTests:46 `ReduceMirrorQuality = true`) — 기본 반전 시 왕복 값이 기본값과 같아져 직렬화 누락을 못 잡음 → false로 뒤집어 유지(관례: "왕복 테스트 값은 기본값과 다르게").
- 기본값 단언 테스트 위치: JsonStateStoreTests(기본값 3종 갱신 이력 — notes 2026-07-17) — 구현 시 해당 테스트에 ReduceMirrorQuality 기본 true 단언 추가/갱신.

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| 서스펜션→재개 후 YT IFrame 플레이어 상태 이상 | 재개 시 재생 불능·상태 꼬임 | 공식 문서상 페이지 상태 보존(탭 sleep과 동일). 재개는 명시 Resume+IsVisible=true. 실동작은 HUMAN-VERIFY(전체화면 진입→해제, 잠금→해제) |
| 서스펜션 중 명령 전송(음소거·자막 등) 시 페이지 스크립트 정지로 처리 지연·유실 | 설정이 화면에 미반영 | PlayerHost.PostCommand가 suspended면 명시 재개 후 전송 (T1 Design — IsSuspended 확인) |
| 서스펜션 중 배경 검은 화면 | 시각 변화 | 질문 라운드 고지·수용 (전체화면·잠금은 안 보임, 배터리 세이버는 절전 취지 부합) |
| 볼륨 디바운스 대기 중(500ms) 앱 종료 시 마지막 볼륨 미저장 | 볼륨 1회분 유실 | Dispose에서 pending 저장 fire-and-forget 시도 + 수용(다음 설정 변경 저장에 자연 회복 — Edge 명시, `.Wait()` 금지 규칙상 동기 대기 안 함) |
| 미러 캡 기본 반전으로 기존 테스트 전제 훼손 | 테스트 의도 상실·오탐 | Harness 명시 고정 false + 왕복 테스트 값 반전 (Investigation 확인 완료) |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
| 심볼 | 영향 받는 파일 | 영향 종류 |
|---|---|---|
| `IPlayerHost.Suspend()`·`ResumeFromSuspend()` (신설) | Services/IPlayerHost.cs, Services/PlayerHost.cs, tests FakePlayer | 인터페이스 확장 — 구현체 2곳 전수 (grep 확인) |
| `PlaybackCoordinator.PolicyPause/ResumeOrSkipFailed` (내부 수정) | Services/PlaybackCoordinator.cs | 내부 구현 — 공개 계약 불변 |
| `PlaybackCoordinator.SetVolumeAsync` (저장 디바운스) | Services/PlaybackCoordinator.cs | 계약 미세 변경: "반환 완료 = 저장 완료" → "저장 예약" (호출자 1곳 — SettingsViewModel fire-and-forget이라 무영향, 명령 적용은 즉시 유지) |
| `AppSettings.ReduceMirrorQuality` 기본값 | Models/AppSettings.cs, tests(Harness·JsonStateStoreTests) | 기본값 반전 — 직렬화 형식 불변, 기존 사용자 저장값 유지 |

### 4-B. 계약·직렬화 변경
- settings.json 형식 불변 (필드 추가·삭제 없음, 기본값만 변경 — 구버전 파일 호환 확인: 미기록 필드는 새 기본 적용, 기록 필드는 유지).
- PlayerCommand 페이로드 불변 (서스펜션은 WebView2 컨트롤 수준 — JS 브리지 무관).

### 4-C. 테스트 파일
- `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` — FakePlayer 확장(suspend/resume 기록), 신규: 정책 일시정지 서스펜션·재개 순서, Harness ReduceMirrorQuality=false 고정
- `tests/DeskTube.Tests/JsonStateStoreTests.cs` — 기본값 단언(true) + 왕복 값 반전(false)

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `IPlayerHost.Suspend/ResumeFromSuspend` | 기존 Pause/Play는 영상 정지(페이지 살아있음) — 렌더러 수준 절전 없음 | WebView2 컨트롤 수준 기능이라 신규 필수. 기존 명령 fire-and-forget 계약(IPlayerHost.cs:29)에 맞춰 void |
| 볼륨 저장 디바운스(CTS+Task.Delay) | 레포 내 디바운스 구현 없음 (grep Debounce/Task.Delay — 재시도 딜레이만 존재) | 사용처 1곳(볼륨)이라 헬퍼 추출 없이 코디네이터 내 인라인 (규칙 5-1 — 3회 문턱 미달) |

### Verified by
- grep "IPlayerHost" → 구현 2곳(PlayerHost·FakePlayer) + 소비 1곳(PlaybackCoordinator) — 전부 T1 Files 포함
- grep "SetVolumeAsync" → 정의 1 + 호출 1(SettingsViewModel:322) + 테스트 1(:355) — 전부 확인
- grep "ReduceMirrorQuality" → 7 hits 전건 문맥 확인 (AppSettings·coordinator 2·SettingsViewModel 3·SettingsPage·테스트 3) — 기본값 외 로직 무변경

## Decisions
### D1. 서스펜션 적용 범위
- **Chosen**: 정책 일시정지 3종(전체화면·배터리 세이버·화면 잠금) 즉시 적용. 사용자 일시정지 비대상.
- **Rationale**: 사용자 확정(질문 라운드). 배터리 세이버 중 검은 배경은 절전 취지 부합으로 수용. 사유 구분 배선 불필요(전체 적용이라 PowerPolicyService 무변경).
- **Source**: 사용자 답변 (2026-07-17), PowerPolicyService.cs 전문

### D2. 서스펜션 API 배치 — IPlayerHost 확장 (코디네이터는 WebView2 비접근)
- **Options**: A) IPlayerHost에 Suspend/ResumeFromSuspend 추가 / B) 코디네이터가 PlayerHost 캐스트해 직접 제어
- **Chosen**: A
- **Rationale**: 코디네이터는 인터페이스에만 의존(IPlayerHost.cs:27 설계 원칙 — WebView2 타입 접근은 PlayerHost만). B는 테스트 불가·계층 위반.
- **Source**: IPlayerHost.cs:26~29

### D3. 서스펜션 중 명령 처리 — PostCommand 자동 재개
- **Chosen**: PlayerHost.PostCommand가 `IsSuspended`면 명시 재개(Resume+IsVisible=true) 후 전송. 재서스펜션은 안 함(Out of Scope).
- **Rationale**: 서스펜션 중 페이지 스크립트 정지라 postMessage 처리가 보장되지 않음(공식 문서 — 상태 변경 API 전 IsSuspended 확인 권장). 명령 유실이 조용한 설정 미반영을 만들므로 근본 차단. 일시정지 중 설정 변경은 드물어 재개 비용 수용.
- **Source**: WebView2 공식 문서(Investigation Log), PlayerHost.cs:130~137

### D4. 재개 지점 — ResumeOrSkipFailed 공통 경로
- **Chosen**: `ResumeOrSkipFailed()` 초입에서 전 플레이어 `ResumeFromSuspend()`. 사용자 `Resume()`·`PolicyResume()` 둘 다 이 경로를 경유하므로 한 곳으로 전 재개 경로 커버(정책 일시정지 중 사용자가 트레이 "재생"으로 직접 재개하는 경로 포함).
- **Source**: PlaybackCoordinator.cs:219~277 직접 Read (두 재개 경로의 합류점 확인)

### D5. 볼륨 저장 디바운스 방식
- **Chosen**: 코디네이터 `SetVolumeAsync` 내부 — 명령 적용(ApplyAudioRouting)은 즉시, 저장은 CTS 교체 + `Task.Delay(500ms)` 후 1회(예약 후 즉시 반환 — 테스트 지연 없음). Dispose에서 pending이면 fire-and-forget 저장 시도.
- **Rationale**: 저장 정책은 설정 소유자(코디네이터) 몫 — VM 디바운스는 다른 호출자가 생기면 우회됨. `.Wait()` 금지 규칙상 Dispose 동기 플러시는 안 하며 0.5초 내 종료 시 볼륨 1회분 유실은 수용(Risks 명시).
- **Source**: PlaybackCoordinator.cs:279~284, SettingsViewModel.cs:321~322(유일 호출자), AGENTS.md 비동기 규칙

### D6. 미러 캡 기본 켬 적용 방식
- **Chosen**: `AppSettings.ReduceMirrorQuality` 기본값만 true로 변경 — 신규 설치·필드 미기록 파일만 적용, 기존 사용자 저장값 유지 (마이그레이션·Normalize 변경 없음).
- **Rationale**: 사용자 확정. FitMode·IsMuted·AutoPlayOnLaunch 기본 변경과 동일한 기존 패턴 (Investigation — 직렬화가 기본값도 기록하므로 기존 파일은 안 덮임).
- **Source**: 사용자 답변 (2026-07-17), AppSettings.cs:30·16·45 선례, StateJsonContext 옵션 확인

### D7. 서스펜션 실패(bool false) 처리
- **Chosen**: 로그만 남기고 무시 — 실패해도 IsVisible=false(렌더링 중단·캐시 정리)의 부분 효과는 유지되고, 재생 동작에는 영향 없음.
- **Source**: WebView2 공식 문서 "best effort — 실패는 오류 아님"(Investigation Log)

## Tasks
- [x] T1. 정책 일시정지 중 WebView2 서스펜션
  - **Type**: D
  - **Design**: ① Services/IPlayerHost.cs + PlayerHost.cs + PlaybackCoordinator.cs ② `IPlayerHost.Suspend()` — "렌더링 중단+렌더러 절전(best-effort)" / `IPlayerHost.ResumeFromSuspend()` — "서스펜션 해제(미서스펜션이면 no-op)". PlayerHost: `Suspend()`= IsVisible=false 후 TrySuspendAsync fire-and-forget(결과 false·예외는 AppLog — D7), `ResumeFromSuspend()`= IsSuspended면 Resume() + IsVisible=true(항상), PostCommand 초입 자동 재개 가드(D3). FakePlayer: `Commands.Add("suspend"/"resume-suspend")` ③ 코디네이터 PolicyPause가 PauseAll 후 SuspendAll, ResumeOrSkipFailed 초입 ResumeAll(D4) — 코디네이터→IPlayerHost 방향 유지(WebView2 비접근, D2) ④ 재서스펜션 로직·서스펜션 상태 이벤트·사유별 분기(PowerPolicyService 변경)는 만들지 않음.
  - **Acceptance**: Given 재생 중, When 정책 일시정지(PolicyPause), Then 전 플레이어에 pause 후 suspend 명령 / When 정책 해제(PolicyResume), Then resume-suspend 후 play 순서 / Given 정책 일시정지 중, When 사용자 Resume(), Then resume-suspend 후 재생 재개 / Given 사용자 Pause(), Then suspend 미발행. 빌드 경고 0 + 기존 테스트 전건 통과 + 신규 테스트 통과. (실제 CPU·메모리 절감과 재개 후 영상 정상 재생은 HUMAN-VERIFY)
  - **Files**:
    - 주: `src/DeskTube/Services/IPlayerHost.cs`, `src/DeskTube/Services/PlayerHost.cs`, `src/DeskTube/Services/PlaybackCoordinator.cs`
    - 테스트: `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` (FakePlayer 확장 + 신규 3~4건)
  - **Edge Cases**:
    - 서스펜션 중 StopAsync → Dispose: 문서상 suspended 상태에서도 Close 가능 — 특별 처리 없음
    - 서스펜션 중 설정 변경(음소거·자막·FitMode·모니터 변경) → PostCommand 자동 재개 후 전송, 재서스펜션 안 함(Out of Scope — 다음 정책 일시정지에서 회복)
    - TrySuspendAsync 실패(false)·예외 → 로그만 (D7), IsVisible=false 효과는 유지
    - 정책 일시정지 직후 즉시 해제(빠른 토글) → TrySuspendAsync 완료 전 ResumeFromSuspend 도착 가능 — Resume은 IsSuspended 확인 후 호출·IsVisible=true는 무조건(자동 재개 규칙과 정합, 문서상 안전)
    - 초기화 전(_controller null) Suspend/Resume → null 조건 접근으로 무시 (PostCommand 선례)
  - **Halt Forecast**:
    - (ii-a) IPlayerHost 공개 인터페이스 확장(계획된 변경 — 구현체 2곳 전수 확인) → `## 사전 승인 항목`
  - **Depends on**: -
- [x] T2. 볼륨 영속화 디바운스
  - **Type**: C
  - **Design**: ① Services/PlaybackCoordinator.cs 단독 ② 신규 private `ScheduleVolumeSave()`(CTS 교체 + Task.Delay(500) 후 SaveSettingsAsync — 취소는 정상 경로, 예외는 AppLog) ③ SetVolumeAsync가 ApplyAudioRouting 즉시 수행 + 저장 예약 후 즉시 반환, Dispose에서 pending CTS 취소 + fire-and-forget 최종 저장(D5) ④ 범용 디바운스 헬퍼·다른 Set* 확대는 하지 않음(호출 빈도 문제는 볼륨뿐).
  - **Acceptance**: Given 연속 SetVolumeAsync N회, Then 볼륨 명령은 N회 즉시 적용(기존 테스트 :348 통과 유지) + 저장은 마지막 값 1회(신규 테스트 — FakeStore 저장 카운트/값 단언, 지연 경과 대기) / 빌드 경고 0.
  - **Files**:
    - 주: `src/DeskTube/Services/PlaybackCoordinator.cs`
    - 테스트: `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` (FakeStore 저장 기록 확장 필요 시 포함)
  - **Edge Cases**:
    - 디바운스 대기 중 StopAsync·다른 설정 저장(SetMutedAsync 등) → 그 저장이 현재 _settings(최신 볼륨 포함)를 통째로 저장 — 유실 자연 회복
    - 대기 중 Dispose(앱 종료) → fire-and-forget 저장 시도, 실패 시 볼륨 1회분 유실 수용(Risks)
    - 연속 호출 경합 → CTS 교체 패턴(CancelMetadataBackfill 선례 — Dispose 없이 Cancel만)
  - **Halt Forecast**: (없음 — 공개 시그니처 불변, 내부 구현 변경)
  - **Depends on**: -
- [x] T3. 미러 화질 캡 기본 켬 (신규 설치)
  - **Type**: C
  - **Acceptance**: Given 필드 미기록·신규 settings, Then ReduceMirrorQuality == true (기본값 단언 테스트) / Given 기존 파일에 false 기록, Then false 유지 (왕복 테스트 — 값을 false로 반전해 직렬화 누락 감지 유지) / Harness는 false 고정으로 기존 미러 테스트 의도 보존, 전건 통과.
  - **Files**:
    - 주: `src/DeskTube/Models/AppSettings.cs`
    - 테스트: `tests/DeskTube.Tests/JsonStateStoreTests.cs`, `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` (Harness 고정 1줄)
  - **Edge Cases**:
    - 구버전 settings.json(필드 미기록) → 역직렬화 시 새 기본 true 적용 (의도 — 신규 기본)
    - 기존 파일 false 명시 기록 → 유지 (Investigation 확인 — 직렬화가 기본값도 기록)
  - **Halt Forecast**: (없음 — 기본값 1줄 + 테스트, 파괴적·의존성 없음)
  - **Depends on**: -
- [x] T4. PRD 보강 + README 갱신
  - **Type**: A
  - **Acceptance**: NFR-1에 "정책 일시정지 중 WebView2 서스펜션(배경 검은 화면 — 렌더링 중단)" 취지 보강, NFR-2에 "미러 화질 하향 기본 켬(2026-07-17 꺼짐에서 변경, 기존 사용자 선택 유지)" 반영 + 변경 이력 1줄 / README "자동 일시정지"·"부하 절감 옵션" 문구 현행화. 문서-코드 역대조 누락·잔존 0.
  - **Files**:
    - 주: `docs/prd.md`, `README.md`
  - **Edge Cases**: (Type A — skip)
  - **Halt Forecast**: (없음 — PRD 보강은 질문 라운드에서 사용자 합의 완료)
  - **Depends on**: T1, T3

## 사전 승인 항목 (일괄 승인 대상)
- T1 — `IPlayerHost` 공개 인터페이스에 `Suspend()`/`ResumeFromSuspend()` 추가 (구현체 2곳 전수 확인 — 계획된 공개 API 변경)
- T2 — `SetVolumeAsync` 저장 시점 계약 변경(즉시 저장 → 500ms 디바운스 예약, 시그니처 불변·호출자 1곳 무영향)
- T4 — PRD NFR-1·NFR-2 보강 (질문 라운드 합의 재확인)

## 불가피한 Halt (위임 불가)
- (없음)

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` → 경고/에러 0
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -p:Platform=x64` → 전건 통과 (기존 117 + 신규)
- 포맷: `dotnet format` 위반 0
- 수동 검증 (HUMAN-VERIFY): ① 재생 중 전체화면 앱 진입 → 작업 관리자에서 WebView2 CPU ~0·메모리 감소 확인, 해제 → 재생 정상 재개 ② 화면 잠금→해제 동일 ③ 배터리 세이버 중 배경 검은 화면(수용 확인) ④ 정책 일시정지 중 트레이 "재생" → 정상 재개 ⑤ 볼륨 드래그 → 소리 즉시 반영·재시작 후 마지막 값 복원 ⑥ 신규 설치(설정 삭제) 후 다중 모니터 재생 → 미러 720 하향 확인

## Phase Ledger
- Phase F 통과 (HEAD 78b8b6e)
- Phase G 통과 (Must 100%)

## Retry Ledger

## Progress Log
- T1-T2 완료 (커밋 b21c3b2, e97cc70): IPlayerHost.Suspend/ResumeFromSuspend + PolicyPause 절전·전 재개 경로 해제·PostCommand 자동 해제 / 볼륨 저장 500ms 디바운스 + Dispose 최종 저장(FireAndForget — quality MAJOR 수정 반영). 빌드 0경고·121/121.
  - 결정: CTS 미Dispose는 CancelMetadataBackfill 관례 승계(follow-up 등록), 타이밍 테스트 실시간 대기 수용(가상 시계 인프라 부재).

## Next Steps
- 권장 다음 액션: 전 task 완료 + Phase F/G 통과 — HUMAN-VERIFY 6건(절전 실효과·재개 정상·검은 배경 수용·트레이 재개·볼륨 복원·미러 하향) 사용자 확인 후, 필요 시 PR 생성·공식 /code-review 호출
- Suggested skills: 공식 /code-review, /security-review (선택)

## Open Questions
- [x] Q1: 서스펜션 적용 범위? → **정책 일시정지 전체(3종) 즉시** 확정 — 사용자 일시정지·사유 구분 기각 (사용자 답변 2026-07-17)
- [x] Q2: 미러 캡 기본값? → **기본 켬(신규 설치만) + PRD·README 갱신** 확정 (사용자 답변 2026-07-17)
