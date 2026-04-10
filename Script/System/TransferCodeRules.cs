using System.Text;
using UnityEngine;

public static class TransferCodeRules
{
    public const int MinLength = 6;
    public const int MaxLength = 16;

    // ASCII letters, digits, and punctuation (33-126 excluding letters/digits).
    private const string AllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

    public static bool Validate(string code, out string error)
    {
        string text = code == null ? string.Empty : code.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Transfer code cannot be empty.";
            return false;
        }
        if (text.Length < MinLength)
        {
            error = $"Transfer code must be at least {MinLength} characters.";
            return false;
        }
        if (text.Length > MaxLength)
        {
            error = $"Transfer code cannot exceed {MaxLength} characters.";
            return false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool isAscii = c >= 33 && c <= 126;
            bool isLetterOrDigit = char.IsLetterOrDigit(c);
            bool isPunctuation = char.IsPunctuation(c);
            if (!isAscii || (!isLetterOrDigit && !isPunctuation))
            {
                error = "Transfer code supports only English letters, digits, and ASCII punctuation.";
                return false;
            }
        }

        error = null;
        return true;
    }

    public static string GenerateRandom()
    {
        int length = Random.Range(MinLength, MaxLength + 1);
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            int idx = Random.Range(0, AllowedChars.Length);
            sb.Append(AllowedChars[idx]);
        }
        return sb.ToString();
    }
}
