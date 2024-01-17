global using Microsoft.Extensions.DependencyInjection;
global using System.Windows;
global using Xunit;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Moq;
global using TableCloth.Resources;

global using TableClothApp = TableCloth.App;
global using static Moq.It;

// https://stackoverflow.com/questions/67647877/problems-running-multiple-xunit-tests-under-sta-thread-wpf
[assembly: CollectionBehavior(DisableTestParallelization = true)]
