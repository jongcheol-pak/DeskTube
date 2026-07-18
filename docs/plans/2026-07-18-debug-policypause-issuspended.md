# Debug: 정책 일시정지 타이머 경로 IsSuspended COMException 크래시

> systematic-debugging **경량 경로** (1-A + Phase 4) — 스택트레이스가 파일·라인·원인을 확정(PlayerHost.cs:197 `get_IsSuspended` → 0x8007139F)했고 수정이 단일 파일·소규모라 Phase 1-B~1-D·2·3 생략.

## Symptom

앱 실행 중 무조작 상태에서 크래시. 사용자 제공 스택트레이스:

- `COMException 0x8007139F` "그룹 또는 리소스가 요청된 작업을 실행할 올바른 상태에 있지 않습니다" (ERROR_RESOURCE_NOT_IN_CORRECT_STATE)
- `CoreWebView2.get_IsSuspended` ← `PlayerHost.PostCommand` (PlayerHost.cs:197) ← `Pause` ← `PlaybackCoordinator.PauseAll` ← `PolicyPause` ← `PowerPolicyService.Evaluate/SetSignal` ← **DispatcherQueueTimer.Tick**

## Reproduction

결정적 재현 불가(WebView2 프로세스가 무효인 짧은 창 + 정책 일시정지 타이머 발화의 타이밍 교차). 재현 조건: WebView2 브라우저/렌더러 프로세스가 죽은 직후(ProcessFailed 처리·재생성 전) `PowerPolicyService` 타이머가 `PolicyPause`를 발화하면 발생.

## Phase 1 — Evidence (1-A)

- **크래시 라인**: `PlayerHost.cs:197` `if (core.IsSuspended)` — 스택의 `get_IsSuspended`와 정확히 일치.
- **가드 누락**: 커밋 `7ae0005`("PlayerHost WebView2 상호작용 예외 방어 보강")가 프로세스 무효 창의 interop 예외를 `TryInteract`로 흡수하도록 했으나, `PostCommand`에서는 `PostWebMessageAsJson`만 감싸고 **`IsSuspended` 조회가 가드 밖**(가드보다 먼저 실행)에 남음. 바로 아래 주석("렌더러 크래시 직후 core가 무효인 짧은 창에서 전송이 던질 수 있음 — 흡수")이 이 실패 부류를 이미 인정하고 있었다.
- **전파 경로**: 타이머 콜백(`DispatcherQueueTimer.Tick`) 안에서 예외를 아무도 안 잡음 → UI 스레드 전역 UnhandledException → 프로세스 종료. `7ae0005`가 막으려던 것과 동일한 크래시 부류.
- **동일 부류 무방어 지점**: 같은 `PolicyPause` 타이머 경로에서 호출되는 `Suspend()`의 `controller.IsVisible = false`(128줄)도 가드 밖 — 이번 크래시 지점만 막으면 다음엔 여기서 터질 구체적 재발 시나리오.

## Phase 3 — Root Cause (스택으로 확정)

WebView2 프로세스가 무효 상태이면 `CoreWebView2`의 **모든 COM 호출이(프로퍼티 getter 포함)** 0x8007139F를 던진다. `PostCommand`의 예외 흡수 가드가 `IsSuspended` getter를 포함하지 않아, 타이머 콜백에서 발생한 예외가 미처리로 전파돼 앱이 종료됐다.

## Phase 4 — Fix

- **변경**: `src/DeskTube/Services/PlayerHost.cs`
  - `PostCommand`: `IsSuspended` 조회 + `ResumeFromSuspend()` + `PostWebMessageAsJson`을 하나의 `TryInteract("플레이어 명령 전송")` 람다로 이동 — 조회 시점부터 흡수.
  - `Suspend`: `controller.IsVisible = false` + `TrySuspendCoreAsync` 호출을 `TryInteract("플레이어 절전")`로 감쌈 (방어 심층화 — 동일 타이머 경로의 동일 부류).
- **Test added**: 없음 — `CoreWebView2`는 sealed WinRT 프로젝션이라 무효 상태를 단위 테스트로 재현 불가(RED 불능). 수동 검증 절차로 대체(아래).
- **잔여(수정 안 함, 기록만)**: `_retryTimer.Tick`·`OnProcessFailed`의 `_core?.Reload()`도 이론상 같은 부류지만, 브라우저 프로세스 실패 시 코디네이터 재생성 경로에서 `Dispose`가 `_core`를 null로 만들어 창이 훨씬 좁음 — 실제 발생 시 동일 패턴으로 감싸면 됨.

## Verification

- Build: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` — 경고 0, 오류 0
- Tests: 125/125 통과
- Review: spec-compliance-reviewer OK (A1~A5 전항 충족) / code-quality-reviewer OK (BLOCKER/MAJOR/MINOR 0 — 중첩 TryInteract·fire-and-forget 안전 확인)
- Manual repro: 결정적 재현 불가 크래시라 **HUMAN-VERIFY** — 장기 실행 중(특히 절전/정책 일시정지 발화 시) 동일 크래시 미재발 관찰
