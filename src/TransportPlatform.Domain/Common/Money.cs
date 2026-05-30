namespace TransportPlatform.Domain.Common;

/// <summary>
/// Money as a value object: an exact decimal amount plus an ISO currency code.
/// Using <see cref="decimal"/> (never float) is mandatory for financial correctness.
/// </summary>
public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new DomainException("money.negative", "Money amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException("money.currency_invalid", "Currency must be a 3-letter ISO code.");

        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Multiply(int quantity)
    {
        if (quantity < 0)
            throw new DomainException("money.negative_quantity", "Quantity cannot be negative.");
        return new Money(Amount * quantity, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException("money.currency_mismatch", "Cannot operate on different currencies.");
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
