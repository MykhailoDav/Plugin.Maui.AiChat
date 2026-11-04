using System.Text;

namespace Plugin.Maui.AiChat.Markdown;

/// <summary>
/// Makes Markdown that is still arriving render the way it will when it is complete. A model writes <c>**bo</c>
/// before <c>ld**</c>; parsed as is, the asterisks show as text and then jump into bold, so the answer flickers.
/// </summary>
/// <remarks>
/// Only the inline state at the end of the text is healed: emphasis, strikethrough, code spans and a link whose URL is
/// still arriving. Block structure is left to the parser, which already renders an open code fence to the end.
/// The delimiter rules follow CommonMark (left- and right-flanking runs, intraword <c>_</c>) closely enough for model
/// output; the parser has the final say on the healed text.
/// </remarks>
internal static class StreamingMarkdown
{
    internal static string Heal(string text)
    {
        var state = Scan(text);
        if (state.InFence)
        {
            return text;
        }

        var withoutOpener = RemoveTrailingOpener(text);
        if (!ReferenceEquals(withoutOpener, text))
        {
            text = withoutOpener;
            state = Scan(text);
        }

        if (!state.LinkOpen && state.CodeLength == 0 && state.Delimiters.Count == 0)
        {
            return text;
        }

        // Closers go before trailing whitespace: a closing run must follow a non-space character.
        var contentEnd = text.AsSpan().TrimEnd().Length;
        var healed = new StringBuilder(text.Length + 8).Append(text, 0, contentEnd);
        if (state.LinkOpen)
        {
            healed.Append(')');
        }
        else if (state.CodeLength > 0)
        {
            healed.Append('`', state.CodeLength);
        }

        for (var i = state.Delimiters.Count - 1; i >= 0; i--)
        {
            healed.Append(state.Delimiters[i].Char, state.Delimiters[i].Count);
        }

        return healed.Append(text, contentEnd, text.Length - contentEnd).ToString();
    }

    // "Hello **" is the start of something whose text has not arrived; closing it would show "****".
    private static string RemoveTrailingOpener(string text)
    {
        var end = text.AsSpan().TrimEnd().Length;
        if (end == 0 || !IsDelimiterOrBacktick(text[end - 1]))
        {
            return text;
        }

        var start = end - 1;
        while (start > 0 && text[start - 1] == text[end - 1])
        {
            start--;
        }

        return start == 0 || char.IsWhiteSpace(text[start - 1]) ? string.Concat(text.AsSpan(0, start), text.AsSpan(end)) : text;
    }

    private static bool IsDelimiterOrBacktick(char c) => c is '*' or '_' or '~' or '`';

    private static ScanState Scan(string text)
    {
        var state = new ScanState();
        var lineStart = 0;
        // The empty line after a final newline has not started yet, so it is not a blank line.
        do
        {
            var lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }

            ScanLine(text.AsSpan(lineStart, lineEnd - lineStart), state);
            lineStart = lineEnd + 1;
        }
        while (lineStart < text.Length);

        return state;
    }

    private static void ScanLine(ReadOnlySpan<char> line, ScanState state)
    {
        // A URL never spans lines, so an open one ended with the previous line.
        state.LinkOpen = false;
        var trimmed = line.TrimStart();

        if (state.InFence)
        {
            if (FenceRun(trimmed, out var fenceChar, out var length) && fenceChar == state.FenceChar
                && length >= state.FenceLength && trimmed[length..].IsWhiteSpace())
            {
                state.InFence = false;
            }

            return;
        }

        if (FenceRun(trimmed, out var openChar, out var openLength))
        {
            state.InFence = true;
            state.FenceChar = openChar;
            state.FenceLength = openLength;
            state.ResetInline();
            return;
        }

        if (trimmed.IsEmpty || StartsBlock(trimmed))
        {
            state.ResetInline();
        }

        ScanInline(line, state);
    }

    private static bool FenceRun(ReadOnlySpan<char> trimmed, out char fenceChar, out int length)
    {
        fenceChar = trimmed.IsEmpty ? '\0' : trimmed[0];
        length = 0;
        if (fenceChar is not ('`' or '~'))
        {
            return false;
        }

        while (length < trimmed.Length && trimmed[length] == fenceChar)
        {
            length++;
        }

        return length >= 3;
    }

    // Lines that start a heading, quote, table row or list item: emphasis does not continue into them.
    private static bool StartsBlock(ReadOnlySpan<char> trimmed)
    {
        var first = trimmed[0];
        if (first is '#' or '>' or '|')
        {
            return true;
        }

        if (first is '-' or '*' or '+')
        {
            return trimmed.Length == 1 || char.IsWhiteSpace(trimmed[1]);
        }

        var digits = 0;
        while (digits < trimmed.Length && digits < 10 && char.IsAsciiDigit(trimmed[digits]))
        {
            digits++;
        }

        return digits > 0 && digits < trimmed.Length && trimmed[digits] is '.' or ')'
            && (digits + 1 == trimmed.Length || char.IsWhiteSpace(trimmed[digits + 1]));
    }

    private static void ScanInline(ReadOnlySpan<char> line, ScanState state)
    {
        var i = 0;
        while (i < line.Length)
        {
            var c = line[i];
            if (c == '`')
            {
                var run = RunLength(line, i);
                state.CodeLength = state.CodeLength == 0 ? run : run == state.CodeLength ? 0 : state.CodeLength;
                i += run;
            }
            else if (state.CodeLength > 0)
            {
                i++;
            }
            else if (c == '\\')
            {
                i += 2;
            }
            else if (c == ']' && i + 1 < line.Length && line[i + 1] == '(')
            {
                var close = line[(i + 2)..].IndexOf(')');
                state.LinkOpen = close < 0;
                i = close < 0 ? line.Length : i + 3 + close;
            }
            else if (c is '*' or '_' or '~')
            {
                var run = RunLength(line, i);
                var before = i > 0 ? line[i - 1] : '\n';
                var after = i + run < line.Length ? line[i + run] : '\n';
                Delimit(state, c, run, before, after);
                i += run;
            }
            else
            {
                i++;
            }
        }
    }

    private static int RunLength(ReadOnlySpan<char> line, int start)
    {
        var end = start + 1;
        while (end < line.Length && line[end] == line[start])
        {
            end++;
        }

        return end - start;
    }

    private static void Delimit(ScanState state, char c, int run, char before, char after)
    {
        if (c == '~' && run != 2)
        {
            return;
        }

        bool beforeSpace = char.IsWhiteSpace(before), afterSpace = char.IsWhiteSpace(after);
        bool beforePunctuation = IsPunctuation(before), afterPunctuation = IsPunctuation(after);
        var leftFlanking = !afterSpace && (!afterPunctuation || beforeSpace || beforePunctuation);
        var rightFlanking = !beforeSpace && (!beforePunctuation || afterSpace || afterPunctuation);
        var canOpen = c == '_' ? leftFlanking && (!rightFlanking || beforePunctuation) : leftFlanking;
        var canClose = c == '_' ? rightFlanking && (!leftFlanking || afterPunctuation) : rightFlanking;

        var remaining = run;
        var stack = state.Delimiters;
        for (var j = stack.Count - 1; canClose && remaining > 0 && j >= 0; j--)
        {
            if (stack[j].Char != c)
            {
                continue;
            }

            // Delimiters between a matched opener and its closer can no longer match anything.
            stack.RemoveRange(j + 1, stack.Count - j - 1);
            var used = Math.Min(remaining, stack[j].Count);
            remaining -= used;
            if (stack[j].Count == used)
            {
                stack.RemoveAt(j);
            }
            else
            {
                stack[j] = (c, stack[j].Count - used);
            }
        }

        if (remaining > 0 && canOpen)
        {
            stack.Add((c, remaining));
        }
    }

    private static bool IsPunctuation(char c) => char.IsPunctuation(c) || char.IsSymbol(c);

    private sealed class ScanState
    {
        public bool InFence;
        public char FenceChar;
        public int FenceLength;
        public bool LinkOpen;
        public int CodeLength;
        public readonly List<(char Char, int Count)> Delimiters = [];

        public void ResetInline()
        {
            CodeLength = 0;
            Delimiters.Clear();
        }
    }
}
