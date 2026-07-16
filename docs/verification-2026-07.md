# DeskTube 최종 통합 검증 기록 (2026-07)

part2 T8 (NFR-2·3·5) 검증 절차와 결과. 자동 검증은 이 세션에서 실측했고,
실기·관리자 권한이 필요한 항목은 절차와 함께 ⏳ HUMAN-VERIFY로 표시한다.

## 자동 검증 결과 (2026-07-16 실측)

| 항목 | 명령 | 결과 |
|---|---|---|
| Debug 전체 재빌드 | `dotnet build DeskTube.slnx -c Debug -p:Platform=x64 --no-incremental` | ✅ 경고 0 · 오류 0 |
| Release 전체 재빌드 | `dotnet build DeskTube.slnx -c Release -p:Platform=x64 --no-incremental` | ✅ 경고 0 · 오류 0 |
| 단위 테스트 | `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -c Debug -p:Platform=x64` | ✅ 81/81 통과 |
| 서식 | `dotnet format DeskTube.slnx --verify-no-changes` | ✅ 변경 0건 |
| 다국어 키 동등성 | en/ko resw `data name` 집합 diff | ✅ 차이 0 (100키) |
| 문구 하드코딩 | XAML/C# 사용자 노출 문자열 grep | ✅ 브랜드명 "DeskTube" 2건만 (번역 비대상 예외) |
| MSIX 패키징 | MSBuild `GenerateAppxPackageOnBuild=true` (Release x64, SideloadOnly) | ✅ `AppPackages/DeskTube_0.1.0.0_x64_Test/DeskTube_0.1.0.0_x64.msix` 생성 |

패키징 참고: manifest StartupTask Extension의 `Executable`은 `$targetnametoken$` 토큰이
치환되지 않아 리터럴(`DeskTube.exe`)이어야 MakeAppx 검증을 통과한다 (T8에서 수정).

## ⏳ HUMAN-VERIFY — 관리자 권한·실기 필요 (NFR-5·2·3)

### 1. WACK (NFR-5) — 관리자 PowerShell에서 실행

도구 확인됨: `C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe`
(실행에 관리자 권한(UAC)이 필요해 자동 세션에서는 수행 불가)

```powershell
# 관리자 PowerShell
& "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe" reset
& "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe" test `
  -appxpackagepath "D:\Personal Project\Windows\DeskTube\AppPackages\DeskTube_0.1.0.0_x64_Test\DeskTube_0.1.0.0_x64.msix" `
  -reportoutputpath "D:\Personal Project\Windows\DeskTube\AppPackages\wack-report.xml"
# 결과: OVERALL_RESULT="PASS" 확인. FAIL 항목이 있으면 항목명을 기록해 수정 요청.
```

- [ ] WACK 통과 (실패 0) — 결과: __________

### 2. 메모리 실측 (NFR-2 — 대기 워킹셋 150MB 이하 목표)

```powershell
# 개발자 모드에서 사이드로드 설치 (또는 VS 배포 후 실행)
Add-AppxPackage -Path "D:\...\AppPackages\DeskTube_0.1.0.0_x64_Test\DeskTube_0.1.0.0_x64.msix" -AllowUnsigned
# 시나리오 A: 재생 2모니터 상태로 5분 → 작업 관리자에서 DeskTube + WebView2 프로세스 합산 기록
# 시나리오 B: 트레이 대기(정지 — WebView2 완전 해제 상태) 5분 → 워킹셋 기록
```

- [ ] 정지 대기 워킹셋: ________ MB (목표 ≤ 150MB — part1 설계: 정지 시 WebView2 완전 해제)
- [ ] 재생(2모니터) 워킹셋: ________ MB (참고 기록)

### 3. 콜드 스타트 (NFR-3 — 3초 이내)

- [ ] 재부팅 후 첫 실행 → 트레이 아이콘 표시까지: ________ 초
- [ ] 창 열기(설정 열기) → 화면 표시까지: ________ 초

### 4. 기능 수동 검증 누적 목록 (T1~T7)

- [ ] T1: 트레이 메뉴 재생/정지/볼륨/종료 각 동작 + 창 X 클릭 시 종료되지 않고 트레이 유지
- [ ] T2: 설정 각 항목 즉시 적용(볼륨·모니터·오디오 대상은 재생 중 반영) + 재시작 후 유지
- [ ] T3: 리스트 생성→URL 3개→순서 변경→재생이 그 순서로 시작, 재시작 후 유지
- [ ] T4: 자동 실행 토글 on + 재부팅(또는 `DeskTube.exe -startup` 시뮬레이션) → 창 없이 트레이 + 자동 재생
- [ ] T5: 로그인 → "로그인됨" 표시 + 광고 미표시 (구글 차단 시: 안내 표시 + 나머지 정상이면 통과)
- [ ] T6: 정보 화면 버전이 manifest(0.1.0.0)와 일치, 라이선스 4건 전문 표시
- [ ] T7: ko↔en 전환 시 미번역(키 노출) 0 + 전환 후 테마 유지
- [ ] part1 이월: 아이콘 뒤 렌더링·종료 복구, 소리 자동재생, 2모니터 동기, 전체화면/잠금/세이버 자동 일시정지, 클린 설치 최초 실행

## README 대조

README.md의 기능 목록 ↔ 구현 대조는 T8 리뷰(spec-compliance)가 코드 기준으로 확인.
