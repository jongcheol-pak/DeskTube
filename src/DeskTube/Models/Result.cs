namespace DeskTube.Models;

/// <summary>서비스 경계 오류 분류 (AGENTS 에러 처리 컨벤션 — 예외 대신 Result).</summary>
public enum ErrorCode
{
    None,

    /// <summary>입력 검증 실패 (빈 이름, 잘못된 URL 등).</summary>
    InvalidInput,

    /// <summary>상한 초과 (플레이리스트 100개 / 항목 1000개 — PRD FR-6).</summary>
    LimitExceeded,

    /// <summary>대상 없음 (삭제된 ID 참조 등).</summary>
    NotFound,

    /// <summary>저장소 접근 실패 (디스크 쓰기 오류 등).</summary>
    StorageFailure,

    /// <summary>실행 환경 실패 (WorkerW 부착 실패 등 OS 상호작용).</summary>
    EnvironmentFailure,
}

/// <summary>값 없는 작업 결과.</summary>
public readonly record struct Result(bool IsSuccess, ErrorCode Code, string? Message)
{
    public static Result Ok() => new(true, ErrorCode.None, null);
    public static Result Fail(ErrorCode code, string? message = null) => new(false, code, message);
}

/// <summary>값 있는 작업 결과. 실패 시 Value는 default.</summary>
public readonly record struct Result<T>(bool IsSuccess, T? Value, ErrorCode Code, string? Message)
{
    public static Result<T> Ok(T value) => new(true, value, ErrorCode.None, null);
    public static Result<T> Fail(ErrorCode code, string? message = null) => new(false, default, code, message);
}
