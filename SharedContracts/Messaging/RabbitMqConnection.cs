using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace SharedContracts.Messaging
{
    public class RabbitMqOptions
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string ExchangeName { get; set; } = "realestate.events";
    }

    public static class MessageSerializer
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

        public static string Serialize<T>(T message) =>
            JsonSerializer.Serialize(message, message!.GetType(), Options);

        public static T Deserialize<T>(string json) =>
            JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new InvalidOperationException($"Empty {typeof(T).Name} message.");
    }

    /// <summary>
    /// One shared RabbitMQ connection per application.
    /// Declares the topology on first connect and publishes
    /// persistent messages with publisher confirms.
    /// </summary>
    public sealed class RabbitMqConnection : IDisposable
    {
        public const string RetryHeader = "x-retry-count";

        private readonly RabbitMqOptions _options;
        private readonly ILogger<RabbitMqConnection> _logger;
        private readonly string _clientName;
        private readonly object _connectLock = new();
        private readonly object _publishLock = new();

        private IConnection? _connection;
        private IModel? _publishChannel;

        public RabbitMqConnection(
            IOptions<RabbitMqOptions> options,
            ILogger<RabbitMqConnection> logger,
            string clientName)
        {
            _options = options.Value;
            _logger = logger;
            _clientName = clientName;
        }

        public string Exchange => _options.ExchangeName;

        public IConnection GetConnection()
        {
            lock (_connectLock)
            {
                // Once created, the connection reconnects by itself (automatic
                // recovery) and also restores the consumers' channels. Replacing
                // it while it recovers would leave the consumers on a dead
                // connection, so callers just wait for the recovery.
                if (_connection != null)
                    return _connection.IsOpen
                        ? _connection
                        : throw new InvalidOperationException("RabbitMQ is not reachable; reconnecting.");

                var factory = new ConnectionFactory
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    UserName = _options.UserName,
                    Password = _options.Password,
                    DispatchConsumersAsync = true,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
                };

                var connection = factory.CreateConnection(_clientName);
                try
                {
                    using var channel = connection.CreateModel();
                    MessagingTopology.Declare(channel, Exchange);
                }
                catch
                {
                    connection.Dispose(); // try again (and declare again) next time
                    throw;
                }
                _connection = connection;

                _logger.LogInformation(
                    "RabbitMQ connected to {Host}:{Port} and topology declared.",
                    _options.HostName, _options.Port);

                return _connection;
            }
        }

        /// <summary>
        /// Publishes a persistent JSON message and waits for the broker confirm.
        /// Throws if the broker does not confirm, so callers can retry.
        /// </summary>
        public void Publish(
            string routingKey,
            Guid messageId,
            string eventType,
            Guid correlationId,
            string json,
            int retryCount = 0)
        {
            lock (_publishLock)
            {
                if (_publishChannel is not { IsOpen: true })
                {
                    var connection = GetConnection(); // throws while RabbitMQ is down; the outbox retries later
                    _publishChannel?.Dispose();
                    _publishChannel = connection.CreateModel();
                    _publishChannel.ConfirmSelect();
                }

                var props = _publishChannel.CreateBasicProperties();
                props.Persistent = true;
                props.ContentType = "application/json";
                props.MessageId = messageId.ToString();
                props.Type = eventType;
                props.CorrelationId = correlationId.ToString();
                props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                props.Headers = new Dictionary<string, object> { [RetryHeader] = retryCount };

                _publishChannel.BasicPublish(
                    Exchange, routingKey, mandatory: false, props, Encoding.UTF8.GetBytes(json));

                _publishChannel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
            }

            _logger.LogInformation(
                "Published {EventType} {MessageId} with routing key {RoutingKey}.",
                eventType, messageId, routingKey);
        }

        public void Dispose()
        {
            _publishChannel?.Dispose();
            _connection?.Dispose();
        }
    }

    public static class MessagingServiceCollectionExtensions
    {
        /// <summary>
        /// Registers RabbitMQ options (section "RabbitMQ") and the shared connection.
        /// </summary>
        public static IServiceCollection AddRabbitMqMessaging(
            this IServiceCollection services,
            IConfiguration configuration,
            string clientName)
        {
            services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));

            services.AddSingleton(sp => new RabbitMqConnection(
                sp.GetRequiredService<IOptions<RabbitMqOptions>>(),
                sp.GetRequiredService<ILogger<RabbitMqConnection>>(),
                clientName));

            return services;
        }
    }
}
