using Elders.Cronus.DomainModelling;

namespace Elders.Cronus.Tests.TestModel
{
    public class TestCreateCommand : ICommand
    {
        public TestAggregateId Id { get; set; }
    }
}