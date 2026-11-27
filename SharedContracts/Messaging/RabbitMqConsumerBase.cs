using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SharedContracts.Messaging
{
    public record ReceivedMessage(
        Guid MessageId,
        string EventType,
        string RoutingKey,
        string Body,
        int RetryCount)
    {
        public T Read<T>() => MessageSerializer.Deserialize<T>(Body);
    }

    /// <summary>
    /// Background consumer with manual acknowledgements.
    ///
    /// - Success: the message is acknowledged.
    /// - Failure: the message is re-published with an incremented
    ///   retry counter (up to MaxRetries), then rejected so RabbitMQ
    ///   moves it to the dead-letter queue.
    ///
    /// Each message is handled in its own DI scope (own DbContext).
    /// Duplicate protection is done by the handler with an Inbox table.
    /// </summary>
    public abstract class RabbitMqConsumerBase : BackgroundService
    {
        private const int MaxRetries = 3;

        private readonly RabbitMqConnection _rabbit;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger _logger;
        private IModel? _channel;

        protected RabbitMqConsumerBase(
            RabbitMqConnection rabbit,
            IServiceScopeFactory scopeFactory,
            ILogger logger)
        {
            _rabbit = rabbit;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected abstract string QueueName { get; }

        protected abstract Task HandleAsync(
            IServiceProvider services,
            ReceivedMessage message,
            CancellationToken cancellationToken);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Connecting is synchronous: leave the startup path first
            // so a down broker never delays the web application.
            await Task.Yield();

            // RabbitMQ may start after the web app: keep trying.
            while (!stoppingToken.IsCancellationRequested && _channel == null)
            {
                try
                {
                    var channel = _rabbit.GetConnection().CreateModel();
                    channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.Received += (_, delivery) => OnReceivedAsync(channel, delivery, stoppingToken);

                    channel.BasicConsume(QueueName, autoAck: false, consumer);
                    _channel = channel;

                    _logger.LogInformation("Consuming RabbitMQ queue {Queue}.", QueueName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "RabbitMQ not reachable ({Error}). Retrying in 5 seconds...", ex.Message);

                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task OnReceivedAsync(
            IModel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
        {
            var props = delivery.BasicProperties;
            var retryCount = ReadRetryCount(props);

            var message = new ReceivedMessage(
                Guid.TryParse(props.MessageId, out var id) ? id : Guid.Empty,
                props.Type ?? string.Empty,
                delivery.RoutingKey,
                Encoding.UTF8.GetString(delivery.Body.Span),
                retryCount);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                await HandleAsync(scope.ServiceProvider, message, stoppingToken);

                channel.BasicAck(delivery.DeliveryTag, multiple: false);

                _logger.LogInformation(
                    "Processed {EventType} {MessageId} from {Queue}.",
                    message.EventType, message.MessageId, QueueName);
            }
            catch (Exception ex)
            {
                if (retryCount < MaxRetries)
                {
                    _logger.LogWarning(ex,
                        "Failed {EventType} {MessageId} (attempt {Attempt}). Retrying.",
                        message.EventType, message.MessageId, retryCount + 1);

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2 * (retryCount + 1)), stoppingToken);

                        _rabbit.Publish(
                            delivery.RoutingKey,
                            message.MessageId,
                            message.EventType,
                            Guid.TryParse(props.CorrelationId, out var c) ? c : Guid.Empty,
                            message.Body,
                            retryCount + 1);

                        channel.BasicAck(delivery.DeliveryTag, multiple: false);
                        return;
                    }
                    catch (Exception republishError)
                    {
                        _logger.LogError(republishError, "Could not re-publish message for retry.");
                    }
                }

                _logger.LogError(ex,
                    "Giving up on {EventType} {MessageId}. Moving it to the dead-letter queue.",
                    message.EventType, message.MessageId);

                // requeue: false => dead-letter exchange => .dlq queue
                channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: false);
            }
        }

        private static int ReadRetryCount(IBasicProperties props)
        {
            if (props.Headers != null &&
                props.Headers.TryGetValue(RabbitMqConnection.RetryHeader, out var value))
            {
                return value switch
                {
                    int i => i,
                    long l => (int)l,
                    byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var p) => p,
                    _ => 0
                };
            }

            return 0;
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            base.Dispose();
        }
    }
}
