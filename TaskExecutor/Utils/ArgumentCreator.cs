namespace TaskExecutor.Utils;

public class ArgumentCreator : List<string>
{
    public ArgumentCreator Append(string argument)
    {
        Add(argument);
        return this;
    }

    /// <summary>
    /// 条件追加：满足条件时才加入参数，避免调用方到处写 if（可选增强）。
    /// </summary>
    public ArgumentCreator AppendIf(bool condition, string argument)
    {
        if (condition)
        {
            Add(argument);
        }

        return this;
    }

    public string ToCommandLine()
    {
        return string.Join(" ", this.Select(QuoteIfNeeded));
    }

    private string QuoteIfNeeded(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        if (argument.Contains(' ') || argument.Contains('\t') || argument.Contains('"'))
        {
            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }

        return argument;
    }
}