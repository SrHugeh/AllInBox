using System;
using System.Collections.Generic;

namespace AllInBox.Models;

public class PlatformStatItem
{
    public string Platform { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public string SummaryText => $"{Count} jogo{(Count != 1 ? "s" : "")} • {Percentage:0.#}%";
    public string BarColor { get; set; } = "#00D4FF";
}

public class RankedGameStatItem
{
    public int Rank { get; set; }
    public string RankBadge { get; set; } = string.Empty;
    public string BadgeBackground { get; set; } = "#1A2230";
    public string BadgeForeground { get; set; } = "#94A3B8";
    public GameItem Game { get; set; } = null!;
    public string ValueFormatted { get; set; } = string.Empty;
    public string PlayTimeFormatted { get; set; } = string.Empty;
    public double ProgressPercentage { get; set; } = 0;
}
