/// A simple lexer implementation from https://stackoverflow.com/a/673657 (Poor man's “lexer” for C#)
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace MetaSprite.Internal {

public interface IMatcher
{
    /// <summary>
    /// Return the number of characters that this "regex" or equivalent
    /// matches.
    /// </summary>
    /// <param name="text">The text to be matched</param>
    /// <returns>The number of characters that matched</returns>
    int Match(string text);
}

sealed class RegexMatcher : IMatcher
{
    private readonly Regex regex;
    public RegexMatcher(string regex)
    {
        this.regex = new Regex(string.Format("^({0})", regex));
    }

    public int Match(string text)
    {
        var m = regex.Match(text);
        return m.Success ? m.Length : 0;
    }

    public override string ToString()
    {
        return regex.ToString();
    }
}

public sealed class TokenDefinition
{
    public readonly IMatcher Matcher;
    public readonly object Token;

    public TokenDefinition(string regex, object token)
    {
        this.Matcher = new RegexMatcher(regex);
        this.Token = token;
    }
}

public sealed class Lexer : IDisposable
{
    private readonly TextReader reader;
    private readonly TokenDefinition[] tokenDefinitions;

    private string lineRemaining;

    public Lexer(TextReader reader, TokenDefinition[] tokenDefinitions)
    {
        this.reader = reader;
        this.tokenDefinitions = tokenDefinitions;
        nextLine();
    }

    private void nextLine()
    {
        do
        {
            lineRemaining = reader.ReadLine();
            ++LineNumber;
            Position = 0;
        } while (lineRemaining != null && lineRemaining.Length == 0);
    }

    public bool Next()
    {
        if (lineRemaining == null)
            return false;
        foreach (var def in tokenDefinitions)
        {
            var matched = def.Matcher.Match(lineRemaining);
            if (matched > 0)
            {
                Position += matched;
                Token = def.Token;
                TokenContents = lineRemaining.Substring(0, matched);
                lineRemaining = lineRemaining.Substring(matched);
                if (lineRemaining.Length == 0)
                    nextLine();

                return true;
            }
        }
        throw new Exception(string.Format("Unable to match against any tokens at line {0} position {1} \"{2}\"",
                                          LineNumber, Position, lineRemaining));
    }

    public string TokenContents { get; private set; }

    public object Token { get; private set; }

    public int LineNumber { get; private set; }

    public int Position { get; private set; }

    public void Dispose()
    {
        reader.Dispose();
    }
}

}