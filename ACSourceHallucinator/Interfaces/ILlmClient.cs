using ACSourceHallucinator.Models;

namespace ACSourceHallucinator.Interfaces;

public interface ILlmClient
{
    Task<LlmResponse> SendRequestAsync(LlmRequest request, CancellationToken ct = default);
}
