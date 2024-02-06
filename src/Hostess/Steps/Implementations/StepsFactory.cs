using Microsoft.Extensions.DependencyInjection;
using System;

namespace Hostess.Steps.Implementations
{
    public sealed class StepsFactory : IStepsFactory
    {
        public StepsFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private readonly IServiceProvider _serviceProvider;

        public IStep GetStepByName(string name)
            => _serviceProvider.GetRequiredKeyedService<IStep>(name);
    }
}
