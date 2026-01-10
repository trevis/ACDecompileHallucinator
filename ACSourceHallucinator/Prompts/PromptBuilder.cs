using System.Text;

namespace ACSourceHallucinator.Prompts;

public class PromptBuilder
{
    private string? _systemMessage;
    private string? _referencesSection;
    private string? _retryFeedback;
    private string? _previousResponse;
    private readonly List<(string Input, string Output)> _fewShotExamples = new();
    private string? _input;
    private string? _targetContext;

    public PromptBuilder WithSystemMessage(string message)
    {
        _systemMessage = message;
        return this;
    }

    public PromptBuilder WithReferences(string references)
    {
        _referencesSection = references;
        return this;
    }

    public PromptBuilder WithTargetContext(string targetContext)
    {
        _targetContext = targetContext;
        return this;
    }

    public PromptBuilder WithRetryFeedback(string? feedback)
    {
        _retryFeedback = feedback;
        return this;
    }

    public PromptBuilder WithPreviousResponse(string? response)
    {
        _previousResponse = response;
        return this;
    }

    public PromptBuilder WithFewShotExample(string input, string output)
    {
        _fewShotExamples.Add((input, output));
        return this;
    }

    public PromptBuilder WithInput(string input)
    {
        _input = input;
        return this;
    }

    public string Build()
    {
        var sb = new StringBuilder();

        if (_systemMessage != null)
        {
            sb.AppendLine(_systemMessage);
            sb.AppendLine();
        }

        if (_targetContext != null)
        {
            sb.AppendLine("=== TARGET CONTEXT ===");
            sb.AppendLine(_targetContext);
            sb.AppendLine();
        }

        if (_referencesSection != null)
        {
            sb.AppendLine("=== REFERENCES ===");
            sb.AppendLine(_referencesSection);
            sb.AppendLine();
        }

        if (_retryFeedback != null)
        {
            sb.AppendLine("=== PREVIOUS ATTEMPT FEEDBACK ===");
            sb.AppendLine(_retryFeedback);
            sb.AppendLine();
        }

        if (_previousResponse != null)
        {
            sb.AppendLine("=== PREVIOUS ATTEMPT OUTPUT ===");
            sb.AppendLine(_previousResponse);
            sb.AppendLine();
        }

        if (_retryFeedback != null || _previousResponse != null)
        {
            sb.AppendLine("Please address the above feedback and correct the previous output in your response.");
            sb.AppendLine();
        }

        if (_fewShotExamples.Any())
        {
            sb.AppendLine("=== EXAMPLES ===");
            foreach (var (input, output) in _fewShotExamples)
            {
                sb.AppendLine($"Input: {input}");
                sb.AppendLine($"Output: {output}");
                sb.AppendLine();
            }
        }

        if (_input != null)
        {
            sb.AppendLine($"Input: {_input}");
            sb.AppendLine($"Output:");
        }

        return sb.ToString();
    }
}
