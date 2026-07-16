using VContainer;
using Core;

public class VContainerInjector : IDependencyInjector
{
    private readonly IObjectResolver _resolver;

    public VContainerInjector(IObjectResolver resolver)
    {
        _resolver = resolver;
    }

    public void Inject(object instance) => _resolver.Inject(instance);
}
