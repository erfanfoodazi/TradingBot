namespace Trading.Shared.Requests;

public sealed class ModifyPositionRequestDto
{
    public long Ticket { get; set; }

    public double? StopLoss { get; set; }

    public double? TakeProfit { get; set; }
}