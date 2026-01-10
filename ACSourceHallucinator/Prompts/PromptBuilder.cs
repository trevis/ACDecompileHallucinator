using System.Text;

namespace ACSourceHallucinator.Prompts;

public class PromptBuilder
{
    private string? _systemMessage;
    private string? _referencesSection;
    private readonly List<string> _retryFeedbacks = new();
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

    public PromptBuilder WithRetryFeedback(IEnumerable<string>? feedbacks)
    {
        if (feedbacks != null)
        {
            _retryFeedbacks.AddRange(feedbacks);
        }

        return this;
    }

    public PromptBuilder WithRetryFeedback(string? feedback)
    {
        if (feedback != null)
        {
            _retryFeedbacks.Add(feedback);
        }

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
            sb.AppendLine("<target_context>");
            sb.AppendLine(_targetContext);
            sb.AppendLine("</target_context>");
            sb.AppendLine();
        }

        if (_referencesSection != null)
        {
            sb.AppendLine("<references>");
            sb.AppendLine(_referencesSection);
            sb.AppendLine("</references>");
            sb.AppendLine();
        }

        if (_retryFeedbacks.Any())
        {
            sb.AppendLine("<previous_attempts>");
            for (int i = 0; i < _retryFeedbacks.Count; i++)
            {
                sb.AppendLine($"<attempt number=\"{i + 1}\">");
                sb.AppendLine(_retryFeedbacks[i]);
                sb.AppendLine("</attempt>");
            }
            sb.AppendLine("</previous_attempts>");
            sb.AppendLine();
        }

        if (_previousResponse != null)
        {
            sb.AppendLine("<previous_output>");
            sb.AppendLine(_previousResponse);
            sb.AppendLine("</previous_output>");
            sb.AppendLine();
        }

        if (_retryFeedbacks.Any() || _previousResponse != null)
        {
            sb.AppendLine("Please address the above feedback and correct the previous output in your response.");
            sb.AppendLine();
        }

        if (_fewShotExamples.Any())
        {
            sb.AppendLine("<examples>");
            for (int i = 0; i < _fewShotExamples.Count; i++)
            {
                var (input, output) = _fewShotExamples[i];
                sb.AppendLine($"<example index=\"{i + 1}\">");
                sb.AppendLine("<input>");
                sb.AppendLine(input);
                sb.AppendLine("</input>");
                sb.AppendLine("<ideal_output>");
                sb.AppendLine(output);
                sb.AppendLine("</ideal_output>");
                sb.AppendLine("</example>");
                sb.AppendLine();
            }
            sb.AppendLine("</examples>");
            sb.AppendLine();
        }

        if (_input != null)
        {
            sb.AppendLine("<instructions_reminder>");
            sb.AppendLine("Generate ONLY XML tags (e.g., <summary>, <param>, <returns>). No markdown formatting.");
            sb.AppendLine("</instructions_reminder>");
            sb.AppendLine();

            sb.AppendLine($"Input: {_input}");
            sb.AppendLine($"Output:");
        }

        return sb.ToString();
    }
}
