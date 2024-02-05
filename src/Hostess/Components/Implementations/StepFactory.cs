using Hostess.Steps;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Hostess.Components.Implementations
{
    public sealed class StepFactory : IStepFactory
    {
        public StepFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private readonly IServiceProvider _serviceProvider;

        public IStep GetStepByName(string name)
            => _serviceProvider.GetRequiredKeyedService<IStep>(name);
    }
}
