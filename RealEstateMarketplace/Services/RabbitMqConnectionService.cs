
using RabbitMQ.Client;

namespace RealEstateMarketplace.Services
{
    public class RabbitMqConnectionService : IDisposable
    {
        private readonly IConfiguration _configuration;
        private IConnection? _connection;

        public RabbitMqConnectionService(
            IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<IConnection> GetConnectionAsync()
        {
            if (_connection?.IsOpen == true)
            {
                return Task.FromResult(_connection);
            }

            var settings = _configuration.GetSection("RabbitMQ");

            var factory = new ConnectionFactory
            {
                HostName = settings["HostName"] ?? "localhost",
                Port = settings.GetValue<int>("Port", 5672),
                UserName = settings["UserName"] ?? "guest",
                Password = settings["Password"] ?? "guest"
            };

            _connection = factory.CreateConnection();

            return Task.FromResult(_connection);
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
