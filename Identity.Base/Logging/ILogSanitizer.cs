using System;

namespace Identity.Base.Logging;

public interface ILogSanitizer
{
    string? RedactEmail(string? value);

    string? RedactPhoneNumber(string? value);

    string RedactToken(string? value);
}
