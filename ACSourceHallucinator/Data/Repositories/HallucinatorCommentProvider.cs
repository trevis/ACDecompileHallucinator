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

    public async Task<string?> GetEnumCommentAsync(int typeId)
    {
        var result = await _stageResultRepository.GetSuccessfulResultAsync("CommentEnums", EntityType.Enum, typeId);
        return result?.GeneratedContent;
    }

    public async Task<string?> GetStructCommentAsync(int typeId)
    {
        var result = await _stageResultRepository.GetSuccessfulResultAsync("CommentStructs", EntityType.Struct, typeId);
        return result?.GeneratedContent;
    }

    public async Task<string?> GetMethodCommentAsync(int methodId)
    {
        var result =
            await _stageResultRepository.GetSuccessfulResultAsync("CommentFunctions", EntityType.StructMethod,
                methodId);
        return result?.GeneratedContent;
    }
}
