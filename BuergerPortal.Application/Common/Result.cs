using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Common
{
    public readonly struct Result<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public string? ErrorCode { get; }
        public string? ErrorMessage { get; }

        private Result(bool ok, T? value, string? code, string? msg)
            => (IsSuccess, Value, ErrorCode, ErrorMessage) = (ok, value, code, msg);

        public static Result<T> Success(T value) => new(true, value, null, null);
        public static Result<T> Fail(string code, string message) => new(false, default, code, message);
    }
    public static class ErrorCodes
    {
        public const string SlotConflict = "slot_conflict";
        public const string Validation = "validation_error";
    }
}
