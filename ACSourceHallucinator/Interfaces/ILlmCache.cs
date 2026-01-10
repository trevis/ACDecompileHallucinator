using ACSourceHallucinator.Models;

namespace ACSourceHallucinator.Interfaces;

public interface ILlmCache
{
    Task<LlmResponse?> GetAsync(LlmRequest request);
    Task SetAsync(LlmRequest request, LlmResponse response);
}
