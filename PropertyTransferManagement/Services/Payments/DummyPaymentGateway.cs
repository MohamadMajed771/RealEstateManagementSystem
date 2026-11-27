namespace PropertyTransferManagement.Services.Payments
{
    public record PaymentResult(bool Succeeded, string? ProviderTransactionId, string? FailureReason);

    /// <summary>
    /// Free stand-in for a real payment provider (no money moves).
    /// Card numbers are checked and then thrown away; only the last
    /// four digits are kept on the payment for the receipt.
    ///
    /// Test cards:
    ///   4242 4242 4242 4242  -> success
    ///   4000 0000 0000 0002  -> declined
    ///   4000 0000 0000 9995  -> insufficient funds
    /// Any other number that passes the Luhn check succeeds.
    /// </summary>
    public class DummyPaymentGateway
    {
        public PaymentResult Charge(decimal amount, string currency, string cardNumber, int expMonth, int expYear, string cvc)
        {
            var digits = new string((cardNumber ?? "").Where(char.IsDigit).ToArray());

            if (digits.Length is < 13 or > 19 || !PassesLuhn(digits))
                return new PaymentResult(false, null, "The card number is not valid.");

            var lastDayOfMonth = new DateTime(expYear, Math.Clamp(expMonth, 1, 12), 1).AddMonths(1).AddDays(-1);
            if (expMonth is < 1 or > 12 || lastDayOfMonth < DateTime.UtcNow.Date)
                return new PaymentResult(false, null, "The card has expired.");

            if (cvc is null || cvc.Length is < 3 or > 4 || !cvc.All(char.IsDigit))
                return new PaymentResult(false, null, "The security code is not valid.");

            if (amount <= 0)
                return new PaymentResult(false, null, "The amount must be greater than zero.");

            return digits switch
            {
                "4000000000000002" => new PaymentResult(false, null, "The card was declined."),
                "4000000000009995" => new PaymentResult(false, null, "Insufficient funds."),
                _ => new PaymentResult(true, "DUMMY-" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(), null)
            };
        }

        public static string LastFour(string cardNumber)
        {
            var digits = new string((cardNumber ?? "").Where(char.IsDigit).ToArray());
            return digits.Length >= 4 ? digits[^4..] : digits;
        }

        private static bool PassesLuhn(string digits)
        {
            int sum = 0;
            bool doubleIt = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int d = digits[i] - '0';
                if (doubleIt) { d *= 2; if (d > 9) d -= 9; }
                sum += d;
                doubleIt = !doubleIt;
            }
            return sum % 10 == 0;
        }
    }
}
