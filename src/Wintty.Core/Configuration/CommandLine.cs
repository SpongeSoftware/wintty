using System.Text;

namespace Wintty.Core.Configuration;

/// <summary>Splits the <c>command</c> option into an executable and its arguments.</summary>
public static class CommandLine
{
    /// <summary>
    /// Splits on whitespace. Double quotes group words (so paths with spaces work) and are removed.
    /// </summary>
    /// <param name="command">The command text.</param>
    /// <returns>The executable and arguments, or <see langword="null"/> when the text is blank.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
    public static (string Application, IReadOnlyList<string> Arguments)? Split(string command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var words = new List<string>();
        var word = new StringBuilder();
        bool quoted = false;
        bool started = false;
        foreach (char c in command)
        {
            if (c == '"')
            {
                quoted = !quoted;
                started = true;
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (started)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    started = false;
                }
            }
            else
            {
                word.Append(c);
                started = true;
            }
        }

        if (started)
        {
            words.Add(word.ToString());
        }

        return words.Count == 0 ? null : (words[0], words.Skip(1).ToList());
    }
}
