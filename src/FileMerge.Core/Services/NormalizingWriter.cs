using System.IO;
using System.Text;

namespace FileMerge.Services;

/// <summary>
/// Writes decoded text to the output while normalizing line endings, optionally dropping the
/// first line of a file, and optionally holding back trailing blank lines until it knows
/// whether more content follows.
/// </summary>
internal sealed class NormalizingWriter
{
    /// <summary>
    /// Swapped between files when the output encoding changes, which happens whenever the
    /// engine is preserving each file's own encoding.
    /// </summary>
    private TextWriter? _writer;

    /// <summary>The line ending to emit, or null to pass each original line ending through.</summary>
    private readonly string? _newline;

    private readonly bool _trimTrailingBlankLines;

    /// <summary>Whitespace-only text held back, in case the file ends here and it must be dropped.</summary>
    private readonly StringBuilder _hold = new();

    private bool _pendingCr;
    private bool _skippingFirstLine;
    private bool _lastEmittedWasNewline = true;
    private bool _wroteAnything;

    public NormalizingWriter(string? newline, bool trimTrailingBlankLines)
    {
        _newline = newline;
        _trimTrailingBlankLines = trimTrailingBlankLines;
    }

    /// <summary>Points the writer at a new sink. Anything held back must be flushed first.</summary>
    public void Attach(TextWriter writer) => _writer = writer;

    /// <summary>Emits anything held back. Call before swapping sinks or finishing the run.</summary>
    public void Flush() => FlushHold();

    public bool AtLineStart => _lastEmittedWasNewline;

    public bool WroteAnything => _wroteAnything;

    /// <summary>Called before each input file. Resets the per-file line state.</summary>
    public void BeginFile(bool skipFirstLine)
    {
        _pendingCr = false;
        _skippingFirstLine = skipFirstLine;
    }

    public void Write(ReadOnlySpan<char> chunk)
    {
        for (int i = 0; i < chunk.Length; i++)
        {
            char ch = chunk[i];

            if (_pendingCr)
            {
                _pendingCr = false;
                if (ch == '\n')
                {
                    EmitNewline("\r\n");
                    continue;
                }

                EmitNewline("\r");
                // Falls through so the current character is still handled below.
            }

            switch (ch)
            {
                case '\r':
                    _pendingCr = true;
                    break;
                case '\n':
                    EmitNewline("\n");
                    break;
                default:
                    EmitText(ch);
                    break;
            }
        }
    }

    /// <summary>
    /// Called after each input file. Flushes a dangling CR, drops any held blank lines, and
    /// guarantees the next file starts on a fresh line when requested.
    /// </summary>
    public void EndFile(bool ensureTrailingNewline)
    {
        if (_pendingCr)
        {
            _pendingCr = false;
            EmitNewline("\r");
        }

        if (_trimTrailingBlankLines)
        {
            _hold.Clear();
        }
        else
        {
            FlushHold();
        }

        if (ensureTrailingNewline && _wroteAnything && !_lastEmittedWasNewline)
        {
            WriteDirect(_newline ?? Environment.NewLine);
            _lastEmittedWasNewline = true;
        }
    }

    /// <summary>Writes literal text (separators, headers) bypassing the per-file line skipping.</summary>
    public void WriteLiteral(string text)
    {
        FlushHold();
        WriteDirect(text);
        if (text.Length > 0)
        {
            _lastEmittedWasNewline = text[^1] is '\n' or '\r';
        }
    }

    public void WriteLineBreak()
    {
        FlushHold();
        WriteDirect(_newline ?? Environment.NewLine);
        _lastEmittedWasNewline = true;
    }

    private void EmitNewline(string original)
    {
        if (_skippingFirstLine)
        {
            _skippingFirstLine = false;
            return;
        }

        string text = _newline ?? original;

        if (_trimTrailingBlankLines)
        {
            _hold.Append(text);
        }
        else
        {
            WriteDirect(text);
        }

        _lastEmittedWasNewline = true;
    }

    private void EmitText(char ch)
    {
        if (_skippingFirstLine)
        {
            return;
        }

        if (_trimTrailingBlankLines && char.IsWhiteSpace(ch))
        {
            // Could still turn out to be the tail of the file, so hold it.
            _hold.Append(ch);
            return;
        }

        FlushHold();
        WriteDirect(ch);
        _lastEmittedWasNewline = false;
    }

    private void FlushHold()
    {
        if (_hold.Length == 0 || _writer is null)
        {
            return;
        }

        foreach (var chunk in _hold.GetChunks())
        {
            _writer.Write(chunk.Span);
        }

        _lastEmittedWasNewline = _hold[^1] is '\n' or '\r';
        _wroteAnything = true;
        _hold.Clear();
    }

    private void WriteDirect(string text)
    {
        _writer?.Write(text);
        _wroteAnything = true;
    }

    private void WriteDirect(char ch)
    {
        _writer?.Write(ch);
        _wroteAnything = true;
    }
}
