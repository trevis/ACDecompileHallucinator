using ACDecompileParser.Shared.Lib.Services;
using ACSourceHallucinator.Data.Repositories;
using ACSourceHallucinator.Enums;

namespace ACSourceHallucinator.Data.Repositories;

public class HallucinatorCommentProvider : ICommentProvider
{
    private readonly IStageResultRepository _stageResultRepository;

    public HallucinatorCommentProvider(IStageResultRepository stageResultRepository)
    {
        _stageResultRepository = stageResultRepository;
    }

    public async Task<string?> GetEnumCommentAsync(string fullyQualifiedName)
    {
        var result =
            await _stageResultRepository.GetSuccessfulResultAsync("CommentEnums", EntityType.Enum, fullyQualifiedName);
        return result?.GeneratedContent;
    }

    public async Task<string?> GetStructCommentAsync(string fullyQualifiedName)
    {
        var result =
            await _stageResultRepository.GetSuccessfulResultAsync("CommentStructs", EntityType.Struct,
                fullyQualifiedName);
        return result?.GeneratedContent;
    }

    public async Task<string?> GetMethodCommentAsync(string fullyQualifiedName)
    {
        var result =
            await _stageResultRepository.GetSuccessfulResultAsync("CommentFunctions", EntityType.StructMethod,
                fullyQualifiedName);
        return result?.GeneratedContent;
    }
}
