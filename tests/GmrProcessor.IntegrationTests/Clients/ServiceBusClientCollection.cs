namespace GmrProcessor.IntegrationTests.Clients;

[CollectionDefinition("ServiceBusClient")]
public class ServiceBusClientCollection : ICollectionFixture<ServiceBusClient>;

[CollectionDefinition("UsesWireMockClient")]
public class WireMockClientCollection : ICollectionFixture<WireMockClient>;

[CollectionDefinition("UsesWireMockAndServiceBusClient")]
public class WireMockAndServiceBusClientCollection
    : ICollectionFixture<WireMockClient>,
        ICollectionFixture<ServiceBusFixture>;
