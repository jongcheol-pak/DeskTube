# Deferred 대장

## 대기
- [2026-07-16] AGENTS.md Test 명령에 `-p:Platform=x64` 추가 — record-project-fact로 사용자 승인 후 갱신 (출처: 2026-07-15-desktube-core-part1 T1)
- [2026-07-16] DI 컨테이너(Microsoft.Extensions.DependencyInjection) 도입 여부 — part2에서도 수동 컴포지션 루트 유지, 의존성 추가는 사용자 승인 필요. 도입·AppServices 재생성 구조 도입 시 PowerPolicyService의 PowerManager 정적 이벤트 해제 로직 동반 필요 (출처: 2026-07-15-desktube-core-part1 D13, F-7 m2)
- [2026-07-16] Microsoft Store 실제 제출(계정·심사·identity 교체) — 앱 완성 후 사용자 수행 (출처: docs/prd.md)
- [2026-07-16] T8 실측 3건 사용자 수행 — WACK(관리자)·대기 워킹셋(≤150MB)·콜드 스타트(≤3초), `docs/verification-2026-07.md` 체크리스트 (출처: 2026-07-15-desktube-ui-part2 T8)
- [2026-07-16] AGENTS.md Build 섹션에 MSIX 패키징 명령(MSBuild 경로 포함) 기록 — record-project-fact 승인 필요 (출처: part2 T8, suggest-agents-record hook)
- [2026-07-16] AGENTS.md 다국어 규칙 3-④ 문구 보정 검토 — "새 Frame 재로드" → "창 재생성"(x:Uid NavigationView 항목 제약) (출처: part2 T7 spec MINOR)
- [2026-07-16] AGENTS.md 디자인 규칙 6에 마스터-디테일 페이지 골격 예외 명시 검토 (출처: part2 T3 quality MINOR)
- [2026-07-16] PlaylistsViewModel Rename/AddItem 실패 안내의 ErrorCode별 분기 — 오류 원인 늘어나면 (출처: part2 T3 quality MINOR)
- [2026-07-16] YouTubeSessionService 상태 변경 이벤트 도입 — 세션 상태 소비자가 늘어나면 (출처: part2 T5 spec MINOR)
- [2026-07-16] AboutViewModel 라이선스 로드 LoadAsync 전환 — 파일 수 늘어나면 (출처: part2 T6 quality MINOR)
- [2026-07-16] 정보 화면 개인정보처리방침 안내를 호스팅 URL로 교체 — Store 제출 시 (출처: part2 T6 quality MINOR, D7)
- [2026-07-16] "마지막 재생 리스트→StartAsync" 로직 3회째 등장 시 공통 헬퍼 추출 (현재 App·TrayIconService 2곳) (출처: part2 T4 quality SUGGEST)
- [2026-07-16] 단일 인스턴스 보장 (Named Mutex + 창 전면화 — 위키 single-instance 패턴): 트레이 상주 + 재실행으로 2인스턴스 공존 관찰 (출처: 2026-07-16-wallpaper-win32-host, debug 부수 관찰)
- [2026-07-16] 전역 예외 훅(UnhandledException 로깅) 상시 탑재 검토 — 위키 global-exception-handling 패턴, AV 조사에서 진단 유효성 확인 (출처: 2026-07-16-wallpaper-win32-host)
- [2026-07-16] 유휴 워킹셋 ~208MB 관찰 — T8 실측 시 NFR-2 목표(150MB)와 대조 (출처: debug 부수 관찰, 기존 "T8 실측 3건" 항목과 함께 수행)

## 종결
- [2026-07-16 → 2026-07-16] part2 plan 실행 — 트레이·설정 UI·플레이리스트 UI·자동 시작·로그인·정보 화면·다국어·최종 검증 — 반영 (2026-07-15-desktube-ui-part2 T1~T8 완료, 실측 3건은 별도 대기 항목으로 분리)
