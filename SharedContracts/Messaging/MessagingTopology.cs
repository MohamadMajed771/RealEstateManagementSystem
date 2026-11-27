using RabbitMQ.Client;
using SharedContracts.Events;

namespace SharedContracts.Messaging
{
    /// <summary>
    /// Exchanges, queues and routing keys shared by both systems.
    /// Both applications declare the full topology at startup
    /// (declarations are idempotent), so a message published while
    /// the other system is offline is kept in its durable queue.
    /// </summary>
    public static class MessagingTopology
    {
        // Queue consumed by System 2 (Transfer Service)
        public const string TransferServiceQueue = "transfer-service.requests";

        // Queue consumed by System 1 (Marketplace)
        public const string MarketplaceQueue = "marketplace.transfer-updates";

        public static class RoutingKeys
        {
            // System 1 -> System 2
            public const string TransferRequested = "marketplace.transfer.requested";
            public const string TransferCancelled = "marketplace.transfer.cancelled";

            // System 2 -> System 1
            public const string TransferCaseCreated = "transfer.case.created";
            public const string TransferStatusChanged = "transfer.case.status-changed";
            public const string AdditionalDocumentsRequired = "transfer.case.documents-required";
            public const string TransferCompleted = "transfer.case.completed";
            public const string TransferRejected = "transfer.case.rejected";
        }

        /// <summary>
        /// Routing key for each event type (the event class name).
        /// </summary>
        public static string RoutingKeyFor(TransferEvent message) => message switch
        {
            TransferRequested => RoutingKeys.TransferRequested,
            TransferCancelled => RoutingKeys.TransferCancelled,
            TransferCaseCreated => RoutingKeys.TransferCaseCreated,
            TransferStatusChanged => RoutingKeys.TransferStatusChanged,
            AdditionalDocumentsRequired => RoutingKeys.AdditionalDocumentsRequired,
            TransferCompleted => RoutingKeys.TransferCompleted,
            TransferRejected => RoutingKeys.TransferRejected,
            _ => throw new ArgumentOutOfRangeException(
                nameof(message), message.GetType().Name, "Unknown event type.")
        };

        public static string DeadLetterExchange(string exchange) => exchange + ".dlx";

        public static string DeadLetterQueue(string queue) => queue + ".dlq";

        /// <summary>
        /// Declares durable exchanges, queues, bindings and dead-letter queues.
        /// </summary>
        public static void Declare(IModel channel, string exchange)
        {
            var dlx = DeadLetterExchange(exchange);

            channel.ExchangeDeclare(exchange, ExchangeType.Topic, durable: true, autoDelete: false);
            channel.ExchangeDeclare(dlx, ExchangeType.Direct, durable: true, autoDelete: false);

            DeclareQueue(channel, exchange, dlx, TransferServiceQueue, "marketplace.transfer.*");
            DeclareQueue(channel, exchange, dlx, MarketplaceQueue, "transfer.case.*");
        }

        private static void DeclareQueue(
            IModel channel, string exchange, string dlx, string queue, string bindingPattern)
        {
            var dlq = DeadLetterQueue(queue);

            channel.QueueDeclare(dlq, durable: true, exclusive: false, autoDelete: false);
            channel.QueueBind(dlq, dlx, routingKey: queue);

            channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object>
                {
                    ["x-dead-letter-exchange"] = dlx,
                    ["x-dead-letter-routing-key"] = queue
                });
            channel.QueueBind(queue, exchange, bindingPattern);
        }
    }
}
