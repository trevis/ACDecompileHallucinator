using ACSourceHallucinator.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ACSourceHallucinator.Pipeline;

public class PipelineBuilder
{
    private readonly List<IStage> _stages = new();
    private readonly IServiceProvider _services;
    
    public PipelineBuilder(IServiceProvider services)
    {
        _services = services;
    }
    
    public PipelineBuilder AddStage<TStage>() where TStage : IStage
    {
        var stage = _services.GetRequiredService<TStage>();
        _stages.Add(stage);
        return this;
    }
    
    public Pipeline Build()
    {
        ValidateDependencies();
        return new Pipeline(_stages);
    }
    
    private void ValidateDependencies()
    {
        var registered = new HashSet<string>();
        foreach (var stage in _stages)
        {
            foreach (var dep in stage.Dependencies)
            {
                if (!registered.Contains(dep))
                    throw new InvalidOperationException(
                        $"Stage '{stage.Name}' depends on '{dep}' which is not registered or comes later");
            }
            registered.Add(stage.Name);
        }
    }
}

public class Pipeline
{
    public IReadOnlyList<IStage> Stages { get; }
    
    public Pipeline(IReadOnlyList<IStage> stages)
    {
        Stages = stages;
    }
}
